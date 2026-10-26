// Job.cs - Điều phối quy trình: sinh input -> chạy lời giải -> ghi thông tin -> ZIP.
using System.Globalization;
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
    public ulong? Seed { get; set; }
    /// <summary>Rỗng = không tạo output.</summary>
    public string SolutionPath { get; set; } = "";
    /// <summary>Thư mục chứa test (đã xử lý trùng tên).</summary>
    public string OutDir { get; set; } = "";
    public ZipMode Zip { get; set; } = ZipMode.Yes;
    /// <summary>Vẫn nén khi bộ test chưa hoàn chỉnh.</summary>
    public bool ZipIncomplete { get; set; }
    public bool ForceRebuild { get; set; }
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
    public string? Error { get; set; }
}

public static class Job
{
    private static string NowStr() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

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
        if (Generator.ValidateFullConfig(p, job.Cfg) is { } ce)
        {
            log.Line("LỖI cấu hình: " + ce);
            result.ExitCode = 2;
            result.Error = ce;
            return result;
        }
        log.Line("");
        log.Line($"=== Sinh test: {p.Title} [{p.Id}] ===");
        log.Line($"  Số test : {job.TestCount}");
        log.Line($"  Seed    : {Util.LL(seed)}  (dùng lại seed này + cùng cấu hình để tạo lại đúng bộ test)");
        log.Line($"  Thư mục : {job.OutDir}");
        log.Write(p.DescribeConfig(job.Cfg));
        ulong est = p.EstimateMaxBytes(job.Cfg);
        if (est > 0)
        {
            double total = (double)est * job.TestCount;
            if (total > 1024.0 * 1024 * 1024)
                log.Line($"  ! Cảnh báo: dung lượng input có thể lên tới ~{Util.FmtBytes((ulong)total)}. Kiểm tra dung lượng ổ đĩa.");
        }

        var gr = new GenerateRequest
        {
            Problem = p,
            Cfg = job.Cfg,
            TestCount = job.TestCount,
            Seed = seed,
            OutDir = job.OutDir,
            Prefix = st.Prefix,
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
        info.Append("Dạng bài      : ").Append(p.Id).Append(" - ").Append(p.Title).Append('\n');
        info.Append("Số test       : ").Append(job.TestCount).Append('\n');
        info.Append("Seed          : ").Append(Util.LL(seed)).Append('\n');
        info.Append("Lời giải chuẩn: ").Append(solutionShown).Append('\n');
        info.Append("Cấu hình:\n").Append(p.DescribeConfig(job.Cfg));
        info.Append("Định dạng input:\n").Append(p.FormatDescription);
        info.Append("Trạng thái    : ").Append(StatusLine(status)).Append('\n');
        if (cancelled) info.Append("  (Người dùng đã hủy giữa chừng.)\n");
        if (compileErr.Length > 0) info.Append("  Lỗi biên dịch/chuẩn bị lời giải:\n").Append(compileErr).Append('\n');
        if (rep.Failures.Count > 0) info.Append("  Test lỗi:\n").Append(FailureSummary(rep));
        foreach (var w in g.Warnings) info.Append("Cảnh báo: ").Append(w).Append('\n');
        foreach (var w in rep.Warnings) info.Append("Cảnh báo: ").Append(w).Append('\n');
        FileManager.WriteFile(Path.Combine(job.OutDir, FileManager.InfoFile), info.ToString());

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
