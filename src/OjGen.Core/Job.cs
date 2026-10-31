// Job.cs - Điều phối quy trình: sinh input -> chạy lời giải -> ghi thông tin -> ZIP.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OjGen.Core;

public enum ZipMode { Yes, No }
public enum ExistingPolicy { Overwrite, NewDir }
public enum SetStatus { Complete, InputOnly, Incomplete }

public sealed class JobSpec
{
    public Problem Problem { get; set; } = null!;
    public Config Cfg { get; set; } = new();
    public int TestCount { get; set; } = 10;
    public int GeneratedTestCount { get; set; }
    public ulong? Seed { get; set; }
    /// <summary>Rỗng = không tạo output.</summary>
    public string SolutionPath { get; set; } = "";
    /// <summary>Thư mục chứa test (đã xử lý trùng tên).</summary>
    public string OutDir { get; set; } = "";
    public ZipMode Zip { get; set; } = ZipMode.Yes;
    /// <summary>Vẫn nén khi bộ test chưa hoàn chỉnh.</summary>
    public bool ZipIncomplete { get; set; }
    public bool ForceRebuild { get; set; }
    public string ProblemName { get; set; } = "";
    public string ProblemCode { get; set; } = "";
    public List<TestGroupSpec> Groups { get; set; } = new();
    public List<ManualTestSpec> ManualTests { get; set; } = new();
}

public sealed class JobResult
{
    /// <summary>0 = hoàn chỉnh hoặc chỉ input, 1 = chưa hoàn chỉnh (lời giải lỗi), 2 = lỗi cấu hình/ghi file.</summary>
    public int ExitCode { get; set; }
    public SetStatus Status { get; set; }
    public bool Cancelled { get; set; }
    public ulong Seed { get; set; }
    public string OutDir { get; set; } = "";
    public string? ZipPath { get; set; }
    public int InputCount { get; set; }
    public OutputReport? Report { get; set; }
    public CoverageReport? Coverage { get; set; }
    public string? Error { get; set; }
}

public static class Job
{
    private static string NowStr() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string NormalizeOutputTokens(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) { pendingSpace = sb.Length > 0; continue; }
            if (pendingSpace) sb.Append(' ');
            sb.Append(c);
            pendingSpace = false;
        }
        return sb.ToString();
    }

    /// <summary>Ghi nhớ lời giải gần nhất vào file cấu hình (không lưu các tùy chọn CLI tạm thời).</summary>
    private static void RememberSolution(Settings st, string path, string problemId)
    {
        try
        {
            var disk = Settings.Load(st.SourcePath, out _);
            if (disk.Solution == path && (problemId.Length == 0 || disk.SolutionFor(problemId) == path)) return;
            disk.Solution = path;
            if (problemId.Length > 0) disk.SolutionByProblem[problemId] = path;
            disk.Save(out _);
            st.Solution = path;
            if (problemId.Length > 0) st.SolutionByProblem[problemId] = path;
        }
        catch (Exception) { }
    }

    public static string StatusLine(SetStatus s) => s switch
    {
        SetStatus.Complete => "HOÀN CHỈNH (đủ .inp và .out)",
        SetStatus.InputOnly => "CHỈ CÓ INPUT (chưa tạo .out vì không dùng lời giải chuẩn)",
        _ => "CHƯA HOÀN CHỈNH (lời giải chuẩn lỗi, thiếu .out)",
    };

    private static void PrintFailures(OutputReport rep, IJobLog log)
    {
        log.Line($"  Các test bị lỗi ({rep.Failures.Count}/{rep.Total}) - KHÔNG có file .out cho các test này:");
        int show = Math.Min(rep.Failures.Count, 30);
        for (int i = 0; i < show; i++) log.Line($"    - {rep.Failures[i].Name}: {rep.Failures[i].Reason}");
        if (rep.Failures.Count > show) log.Line($"    ... và {rep.Failures.Count - show} test khác.");
    }

    private static string FailureSummary(OutputReport rep)
    {
        var sb = new StringBuilder();
        foreach (var f in rep.Failures) sb.Append("    - ").Append(f.Name).Append(": ").Append(f.Reason.Split('\n')[0]).Append('\n');
        return sb.ToString();
    }

    private static bool DecideZip(ZipMode mode, SetStatus status, bool zipIncomplete, IJobLog log)
    {
        if (mode == ZipMode.No) return false;
        if (status == SetStatus.Incomplete)
        {
            if (!zipIncomplete)
            {
                log.Line("Không tạo ZIP vì bộ test chưa hoàn chỉnh (dùng --zip-incomplete nếu vẫn muốn nén).");
                return false;
            }
            return true;
        }
        return true;
    }

    private static string ZipPathFor(string dir) => dir.TrimEnd('/', '\\') + ".zip";

    private static bool VerifyPackageChecksums(IReadOnlyList<ZipEntrySpec> entries, out string? err)
    {
        err = null;
        const string manifestName = "PACKAGE_SHA256SUMS.txt";
        var manifestEntry = entries.FirstOrDefault(entry => string.Equals(entry.Name, manifestName, StringComparison.OrdinalIgnoreCase));
        if (manifestEntry == null) return true;
        try
        {
            var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(manifestEntry.File, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                int separator = raw.IndexOf("  ", StringComparison.Ordinal);
                if (separator != 64 || raw.Length <= separator + 2)
                { err = "Manifest gói có dòng sai định dạng."; return false; }
                string hash = raw[..separator];
                string name = raw[(separator + 2)..];
                if (hash.Any(c => !Uri.IsHexDigit(c)) || Path.GetFileName(name) != name ||
                    !expected.TryAdd(name, hash))
                { err = "Manifest gói có tên file hoặc mã băm không hợp lệ."; return false; }
            }
            var sourceEntries = entries.Where(entry => !string.Equals(entry.Name, manifestName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (expected.Count != sourceEntries.Count)
            { err = $"Manifest gói ghi {expected.Count} file nhưng ZIP sẽ chứa {sourceEntries.Count} file khác manifest."; return false; }
            foreach (var entry in sourceEntries)
            {
                if (!expected.TryGetValue(entry.Name, out string? wanted))
                { err = "Manifest gói thiếu file " + entry.Name + "."; return false; }
                using var stream = File.OpenRead(entry.File);
                string actual = Convert.ToHexString(SHA256.HashData(stream));
                if (!string.Equals(actual, wanted, StringComparison.OrdinalIgnoreCase))
                { err = "SHA-256 gói không khớp: " + entry.Name + " đã thay đổi."; return false; }
            }
            return true;
        }
        catch (Exception ex) { err = "Không đọc/kiểm tra được manifest gói: " + ex.Message; return false; }
    }

    /// <summary>Đóng gói ZIP một thư mục test có sẵn (.inp và .out).</summary>
    public static bool ZipTestFolder(string dir, Settings st, IJobLog log, out string? zipPath)
    {
        zipPath = null;
        var inputs = FileManager.ListFilesWithExt(dir, ".inp");
        if (inputs.Count == 0)
        {
            log.Line($"Thư mục {dir} không có file .inp nào để nén.");
            return false;
        }
        string? checksumError = FileManager.VerifyTestChecksumManifest(dir, out _);
        if (checksumError == "Chưa có manifest SHA-256.")
        {
            if (!FileManager.WriteTestChecksumManifest(dir, out int written, out checksumError))
            {
                log.Line("LỖI: " + checksumError);
                return false;
            }
            log.Line($"Đã ghi manifest SHA-256 mới cho {written} file test.");
        }
        else if (checksumError != null)
        {
            log.Line("LỖI: không đóng gói bộ test đã thay đổi hoặc hỏng: " + checksumError);
            return false;
        }
        var entries = new List<ZipEntrySpec>();
        var missing = new List<string>();
        int outs = 0;
        foreach (var inp in inputs)
        {
            entries.Add(new ZipEntrySpec(Path.GetFileName(inp), inp));
            string o = Path.ChangeExtension(inp, ".out");
            if (File.Exists(o))
            {
                entries.Add(new ZipEntrySpec(Path.GetFileName(o), o));
                outs++;
            }
            else missing.Add(Path.GetFileNameWithoutExtension(inp));
        }
        string checksumManifest = Path.Combine(dir, FileManager.ChecksumFile);
        if (File.Exists(checksumManifest)) entries.Add(new ZipEntrySpec(FileManager.ChecksumFile, checksumManifest));
        foreach (string sidecar in new[] { FileManager.InfoFile, "AUDIT_REPORT.txt", "PACKAGE.txt", "PACKAGE_SHA256SUMS.txt", "problem.ojdraft.json" })
        {
            string sidecarPath = Path.Combine(dir, sidecar);
            if (File.Exists(sidecarPath)) entries.Add(new ZipEntrySpec(sidecar, sidecarPath));
        }
        if (!VerifyPackageChecksums(entries, out string? packageChecksumError))
        {
            log.Line("LỖI: không đóng gói được vì " + packageChecksumError);
            return false;
        }
        string zip = ZipPathFor(dir);
        if (!ZipService.Create(zip, entries, st.ZipCompress, out var zs, out string? err))
        {
            log.Line("LỖI tạo ZIP: " + err);
            return false;
        }
        if (!ZipService.Verify(zip, entries, out err))
        {
            log.Line($"LỖI: file ZIP vừa tạo không vượt qua kiểm tra ({err}). Đã xóa file ZIP.");
            try { File.Delete(zip); } catch (Exception) { }
            return false;
        }
        log.Line("Đã tạo ZIP: " + zip);
        log.Line($"  Gồm {inputs.Count} file .inp và {outs} file .out (dữ liệu {Util.FmtBytes(zs.RawBytes)}, file ZIP {Util.FmtBytes(zs.ZipBytes)}). " +
                 "Đã kiểm tra: mở được, đủ file, đúng nội dung.");
        if (outs == 0) log.Line("  Lưu ý: ZIP CHỈ chứa input, chưa có output.");
        else if (missing.Count > 0) log.Line($"  Lưu ý: {missing.Count} test thiếu .out (vd {missing[0]}).");
        zipPath = zip;
        return true;
    }

    /// <summary>
    /// Chuẩn bị thư mục chứa test khi thư mục (hoặc file .zip cùng tên) đã có dữ liệu.
    /// Có thể đổi dir (NewDir). Trả về false nếu lỗi.
    /// </summary>
    public static bool PrepareOutDir(ref string dir, ExistingPolicy policy, IJobLog log, out string? err)
    {
        err = null;
        string zip = ZipPathFor(dir);
        int n = FileManager.CountTestFiles(dir);
        bool zipExists = File.Exists(zip);
        if (n == 0 && !zipExists) return true;
        if (policy == ExistingPolicy.NewDir)
        {
            dir = FileManager.UniqueDir(dir);
            log.Line("Dùng thư mục mới: " + dir);
            return true;
        }
        long removed = FileManager.ClearTestFiles(dir, out err);
        if (removed < 0) return false;
        if (zipExists)
        {
            try { File.Delete(zip); }
            catch (Exception) { err = "Không xóa được file ZIP cũ " + zip; return false; }
        }
        log.Line($"Ghi đè: đã xóa {removed} file test cũ" + (zipExists ? " và file ZIP cũ" : "") + ".");
        return true;
    }

    public static JobResult Run(JobSpec job, Settings st, IJobLog log, CancellationToken ct = default)
    {
        var p = job.Problem;
        var result = new JobResult { OutDir = job.OutDir };
        ulong seed = job.Seed ?? Generator.RandomSeed();
        result.Seed = seed;
        var groups = job.Groups.Count > 0
            ? job.Groups
            : new List<TestGroupSpec> { new() { Name = "Tất cả", TestCount = job.GeneratedTestCount > 0 ? job.GeneratedTestCount : Math.Max(1, job.TestCount - job.ManualTests.Count), Cfg = job.Cfg } };
        foreach (var group in groups)
        {
            if (Generator.ValidateFullConfig(p, group.Cfg) is { } ce)
            {
                log.Line($"LỖI cấu hình nhóm '{group.Name}': " + ce);
                result.ExitCode = 2;
                result.Error = ce;
                return result;
            }
        }
        string shownTitle = job.ProblemName.Trim().Length == 0 ? p.Title : job.ProblemName.Trim();
        log.Line("");
        log.Line($"=== Sinh test: {shownTitle} [{(job.ProblemCode.Length == 0 ? p.Id : job.ProblemCode)}] ===");
        log.Line($"  Số test : {job.TestCount}");
        log.Line($"  Seed    : {Util.LL(seed)}  (dùng lại seed này + cùng cấu hình để tạo lại đúng bộ test)");
        log.Line($"  Thư mục : {job.OutDir}");
        if (job.Groups.Count == 0) log.Write(p.DescribeConfig(job.Cfg));
        else
        {
            log.Line("  Kế hoạch nhóm:");
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                ulong groupSeed = Generator.GroupSeed(seed, i, group.Seed);
                log.Line($"    {i + 1}. {group.Name}: {group.TestCount} test, seed {Util.LL(groupSeed)}");
                log.Write(p.DescribeConfig(group.Cfg));
            }
        }
        double estimatedBytes = 0;
        foreach (var group in groups)
            estimatedBytes += (double)p.EstimateMaxBytes(group.Cfg) * group.TestCount;
        ulong est = estimatedBytes >= ulong.MaxValue ? ulong.MaxValue : (ulong)estimatedBytes;
        if (est > 0)
        {
            if (est > 1024UL * 1024 * 1024)
                log.Line($"  ! Cảnh báo: dung lượng input có thể lên tới ~{Util.FmtBytes(est)}. Kiểm tra dung lượng ổ đĩa.");
        }

        var gr = new GenerateRequest
        {
            Problem = p,
            Cfg = job.Cfg,
            TestCount = job.GeneratedTestCount > 0 ? job.GeneratedTestCount : Math.Max(1, job.TestCount - job.ManualTests.Count),
            Seed = seed,
            OutDir = job.OutDir,
            Prefix = st.Prefix,
            Groups = job.Groups,
            ManualTests = job.ManualTests,
        };
        GenerateResult g = Generator.GenerateInputs(gr, log, ct);
        if (!g.Ok)
        {
            log.Line("");
            log.Line((g.Cancelled ? "ĐÃ HỦY: " : "LỖI: ") + g.Error);
            result.ExitCode = 2;
            result.Cancelled = g.Cancelled;
            result.Error = g.Error;
            return result;
        }
        result.InputCount = g.Inputs.Count;
        log.Line($"Đã tạo {g.Inputs.Count} file .inp (mỗi file đã được kiểm tra đúng định dạng và giới hạn).");
        foreach (var w in g.Warnings) log.Line("  ! " + w);
        CoverageReport coverage = CoverageAnalyzer.Analyze(p, g.Inputs, groups, job.ManualTests);
        result.Coverage = coverage;
        log.Line("");
        log.Line("--- Độ phủ test và gợi ý ---");
        log.Write(coverage.ToText());

        SetStatus status = SetStatus.InputOnly;
        OutputReport rep = new();
        string compileErr = "";
        string solutionShown = job.SolutionPath.Length == 0 ? "(không dùng)" : job.SolutionPath;
        bool cancelled = false;
        if (job.SolutionPath.Length == 0)
        {
            log.Line("Không dùng lời giải chuẩn: chỉ có file .inp, CHƯA tạo file .out.");
        }
        else
        {
            RememberSolution(st, job.SolutionPath, p.Id);
            log.Line("");
            log.Line("--- Tạo output bằng lời giải chuẩn ---");
            PreparedSolution ps = SolutionRunner.Prepare(job.SolutionPath, st, st.WorkDir, job.ForceRebuild, log, ct);
            if (!ps.Ok)
            {
                compileErr = ps.Error;
                log.Line(ps.Error);
                log.Line("Dừng tạo output. Các file .inp vẫn được giữ lại; sửa lời giải rồi dùng chức năng tạo lại output (--regen-out).");
                status = SetStatus.Incomplete;
            }
            else
            {
                log.Line($"Chạy lời giải trên {g.Inputs.Count} test (timeout {st.TimeoutMs} ms/test):");
                rep = SolutionRunner.ProduceOutputs(ps.Argv, g.Inputs, st, st.WorkDir, log, ct);
                for (int i = 0; i < Math.Min(job.ManualTests.Count, g.Inputs.Count); i++)
                {
                    var manual = job.ManualTests[i];
                    if (!manual.IsSample || manual.ExpectedOutput.Trim().Length == 0) continue;
                    string outPath = Path.ChangeExtension(g.Inputs[i], ".out");
                    if (!File.Exists(outPath)) continue;
                    string actual = FileManager.ReadText(outPath);
                    if (NormalizeOutputTokens(actual) != NormalizeOutputTokens(manual.ExpectedOutput))
                    {
                        string warning = $"Sample '{manual.Name}': output nhập tay khác output của lời giải chuẩn.";
                        coverage.Suggestions.Add(warning);
                        log.Line("  ! " + warning);
                    }
                }
                cancelled = rep.Cancelled;
                status = rep.Failures.Count == 0 && !rep.Cancelled ? SetStatus.Complete : SetStatus.Incomplete;
                foreach (var w in rep.Warnings) log.Line("  ! " + w);
                if (rep.Failures.Count > 0) PrintFailures(rep, log);
            }
        }
        result.Report = rep;
        result.Status = status;
        result.Cancelled = cancelled;

        var info = new StringBuilder();
        info.Append("ojgen ").Append(Util.Version).Append(" - thông tin bộ test\n");
        info.Append("Thời gian tạo : ").Append(NowStr()).Append('\n');
        info.Append("Dạng bài      : ").Append(p.Id).Append(" - ").Append(shownTitle).Append('\n');
        if (job.ProblemCode.Length > 0) info.Append("Mã bài        : ").Append(job.ProblemCode).Append('\n');
        info.Append("Số test       : ").Append(job.TestCount).Append('\n');
        info.Append("Seed          : ").Append(Util.LL(seed)).Append('\n');
        info.Append("Lời giải chuẩn: ").Append(solutionShown).Append('\n');
        if (job.ManualTests.Count > 0)
        {
            info.Append("Test nhập tay:\n");
            for (int i = 0; i < job.ManualTests.Count; i++)
            {
                var manual = job.ManualTests[i];
                string fileName = FileManager.TestName(st.Prefix, i + 1, FileManager.TestNumberWidth(job.TestCount));
                info.Append("  ").Append(fileName).Append(": ").Append(manual.Name)
                    .Append(manual.IsSample ? " [sample]" : "").Append(" · ").Append(manual.ConfigName).Append('\n');
            }
        }
        if (job.Groups.Count == 0) info.Append("Cấu hình:\n").Append(p.DescribeConfig(job.Cfg));
        else
        {
            info.Append("Kế hoạch nhóm:\n");
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                info.Append("Nhóm ").Append(i + 1).Append(": ").Append(group.Name)
                    .Append(" ( ").Append(group.TestCount).Append(" test, seed ")
                    .Append(Util.LL(Generator.GroupSeed(seed, i, group.Seed))).Append(" )\n")
                    .Append(p.DescribeConfig(group.Cfg));
            }
        }
        info.Append("Định dạng input:\n").Append(p.FormatDescription);
        info.Append("\nĐộ phủ test và gợi ý:\n").Append(coverage.ToText()).Append('\n');
        info.Append("Trạng thái    : ").Append(StatusLine(status)).Append('\n');
        if (cancelled) info.Append("  (Người dùng đã hủy giữa chừng.)\n");
        if (compileErr.Length > 0) info.Append("  Lỗi biên dịch/chuẩn bị lời giải:\n").Append(compileErr).Append('\n');
        if (rep.Failures.Count > 0) info.Append("  Test lỗi:\n").Append(FailureSummary(rep));
        foreach (var w in g.Warnings) info.Append("Cảnh báo: ").Append(w).Append('\n');
        foreach (var w in rep.Warnings) info.Append("Cảnh báo: ").Append(w).Append('\n');
        FileManager.WriteFile(Path.Combine(job.OutDir, FileManager.InfoFile), info.ToString());
        if (FileManager.WriteTestChecksumManifest(job.OutDir, out int checksumCount, out string? checksumError))
            log.Line($"Đã ghi SHA-256 cho {checksumCount} file test vào {FileManager.ChecksumFile}.");
        else log.Line("  ! " + checksumError);

        log.Line("");
        log.Line((cancelled ? "ĐÃ HỦY. " : "") + "KẾT QUẢ: " + StatusLine(status));
        if (status == SetStatus.Complete)
            log.Line($"  {rep.Ok}/{rep.Total} test có output, thời gian chạy lâu nhất {rep.MaxMs} ms.");
        else if (status == SetStatus.Incomplete && rep.Total > 0)
            log.Line($"  {rep.Ok}/{rep.Total} test có output.");
        log.Line($"  Thư mục: {job.OutDir}  (thông tin chi tiết: {FileManager.InfoFile})");

        if (!cancelled && DecideZip(job.Zip, status, job.ZipIncomplete, log) && ZipTestFolder(job.OutDir, st, log, out var zp))
            result.ZipPath = zp;
        result.ExitCode = status == SetStatus.Incomplete ? 1 : 0;
        return result;
    }

    /// <summary>Chạy lời giải cho thư mục test có sẵn (tạo lại toàn bộ .out).</summary>
    public static JobResult RegenerateOutputs(string dir, string solution, Settings st, ZipMode zip, bool forceRebuild,
                                              bool zipIncomplete, IJobLog log, CancellationToken ct = default)
    {
        var result = new JobResult { OutDir = dir };
        var inputs = FileManager.ListFilesWithExt(dir, ".inp");
        if (inputs.Count == 0)
        {
            log.Line($"LỖI: thư mục {dir} không có file .inp nào.");
            result.ExitCode = 2;
            result.Error = "Không có file .inp";
            return result;
        }
        result.InputCount = inputs.Count;
        log.Line($"Tạo lại output cho {inputs.Count} file .inp trong {dir}");
        PreparedSolution ps = SolutionRunner.Prepare(solution, st, st.WorkDir, forceRebuild, log, ct);
        SetStatus status;
        OutputReport rep = new();
        string note = "";
        if (!ps.Ok)
        {
            log.Line(ps.Error);
            status = SetStatus.Incomplete;
            note = ps.Error;
        }
        else
        {
            RememberSolution(st, solution, "");
            rep = SolutionRunner.ProduceOutputs(ps.Argv, inputs, st, st.WorkDir, log, ct);
            foreach (var w in rep.Warnings) log.Line("  ! " + w);
            if (rep.Failures.Count > 0) PrintFailures(rep, log);
            status = rep.Failures.Count == 0 && !rep.Cancelled ? SetStatus.Complete : SetStatus.Incomplete;
        }
        string old = FileManager.ReadText(Path.Combine(dir, FileManager.InfoFile));
        var info = new StringBuilder();
        info.Append(old).Append("\n--- Tạo lại output lúc ").Append(NowStr()).Append(" ---\n");
        info.Append("Lời giải chuẩn: ").Append(solution).Append('\n');
        info.Append("Trạng thái    : ").Append(StatusLine(status)).Append('\n');
        if (note.Length > 0) info.Append(note).Append('\n');
        if (rep.Failures.Count > 0) info.Append("  Test lỗi:\n").Append(FailureSummary(rep));
        FileManager.WriteFile(Path.Combine(dir, FileManager.InfoFile), info.ToString());
        if (FileManager.WriteTestChecksumManifest(dir, out int checksumCount, out string? checksumError))
            log.Line($"Đã cập nhật SHA-256 cho {checksumCount} file test.");
        else log.Line("  ! " + checksumError);
        log.Line("KẾT QUẢ: " + StatusLine(status));
        result.Status = status;
        result.Report = rep;
        result.Cancelled = rep.Cancelled;
        if (!rep.Cancelled && DecideZip(zip, status, zipIncomplete, log) && ZipTestFolder(dir, st, log, out var zp))
            result.ZipPath = zp;
        result.ExitCode = status == SetStatus.Incomplete ? 1 : 0;
        return result;
    }
}
