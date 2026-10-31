using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using OjGen.Core;

namespace OjGen.Cli;

internal enum CliAuditLevel { Pass, Info, Warning, Error }

internal sealed record CliAuditItem(CliAuditLevel Level, string Title, string Detail);

internal sealed class CliAuditReport
{
    public List<CliAuditItem> Items { get; } = new();
    public bool Cancelled { get; set; }
    public bool HasErrors => Items.Any(item => item.Level == CliAuditLevel.Error);
    public int WarningCount => Items.Count(item => item.Level == CliAuditLevel.Warning);
    public void Add(CliAuditLevel level, string title, string detail) => Items.Add(new(level, title, detail));

    public string ToText()
    {
        string status = Cancelled ? "ĐÃ HỦY" : HasErrors ? "CHƯA ĐẠT" : WarningCount > 0 ? "ĐẠT CÓ LƯU Ý" : "ĐẠT";
        var sb = new StringBuilder().Append("KIỂM ĐỊNH PHÁT HÀNH: ").Append(status).Append("\n\n");
        foreach (var item in Items)
        {
            string icon = item.Level switch
            {
                CliAuditLevel.Pass => "[ĐẠT]",
                CliAuditLevel.Warning => "[LƯU Ý]",
                CliAuditLevel.Error => "[LỖI]",
                _ => "[INFO]",
            };
            sb.Append(icon).Append(' ').Append(item.Title).Append('\n');
            if (item.Detail.Length > 0) sb.Append("      ").Append(item.Detail.Replace("\n", "\n      ")).Append('\n');
        }
        if (Cancelled) sb.Append("\nĐã hủy kiểm định; không tạo ZIP phát hành.\n");
        return sb.ToString();
    }
}

internal sealed class CliStatementDraft
{
    public int Version { get; set; } = 1;
    public string ProblemId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int TimeLimitMs { get; set; } = 2000;
    public int MemoryLimitMb { get; set; } = 256;
    public string Description { get; set; } = "";
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
    public string Constraints { get; set; } = "";
    public string Examples { get; set; } = "";
}

internal sealed class CliAuditOptions
{
    public required Problem Problem { get; init; }
    public required Config Config { get; init; }
    public required Settings Settings { get; init; }
    public required string TestDirectory { get; init; }
    public int ExpectedTestCount { get; init; }
    public string ReferenceSolution { get; init; } = "";
    public string ValidatorPath { get; init; } = "";
    public string CheckerPath { get; init; } = "";
    public string CandidateSolution { get; init; } = "";
    public string StatementPath { get; init; } = "";
    public ulong? Seed { get; init; }
    public ulong? DifferentialSeed { get; init; }
    public JobSpec? GeneratedSpec { get; init; }
}

internal static class CliReleaseAudit
{
    private const string PackageChecksumFile = "PACKAGE_SHA256SUMS.txt";

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static Config ConfigFor(Config fallback, JobSpec? spec, int testIndex)
    {
        if (spec == null || spec.Groups.Count == 0) return fallback;
        int generatedIndex = testIndex;
        foreach (var group in spec.Groups)
        {
            if (generatedIndex < group.TestCount) return group.Cfg;
            generatedIndex -= group.TestCount;
        }
        return fallback;
    }

    private static List<string> InvalidVariants(string input, Problem problem, Config config)
    {
        var candidates = new List<string> { "", input + "OJGEN_INVALID_TRAILING_TOKEN\n", "OJGEN_INVALID\n" };
        string trimmed = input.TrimEnd('\r', '\n');
        int lastWhitespace = trimmed.LastIndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        if (lastWhitespace >= 0) candidates.Add(trimmed[..(lastWhitespace + 1)].TrimEnd('\r', '\n') + "\n");
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        return candidates.Where(candidate => distinct.Add(candidate) && problem.ValidateInput(candidate, config) != null)
            .Take(3).ToList();
    }

    private static RunLimits Limits(Settings settings) => new()
    {
        TimeoutMs = Math.Clamp(settings.TimeoutMs, 1, 3600000),
        MaxOutputBytes = (ulong)Math.Clamp(settings.MaxOutputMB, 1, 4096) << 20,
        MemoryBytes = settings.MemoryMB <= 0 ? 0UL : (ulong)Math.Min(settings.MemoryMB, 1048576) << 20,
    };

    private static void AuditStatement(string path, Problem problem, CliAuditReport report)
    {
        if (path.Trim().Length == 0)
        {
            report.Add(CliAuditLevel.Warning, "Nội dung đề", "Chưa chọn bản nháp .ojdraft.json; phần mô tả đề chưa được kiểm định.");
            return;
        }
        if (!File.Exists(path))
        {
            report.Add(CliAuditLevel.Error, "Nội dung đề", "Không tìm thấy bản nháp: " + path);
            return;
        }
        try
        {
            var doc = JsonSerializer.Deserialize<CliStatementDraft>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (doc == null || doc.Version != 1) throw new InvalidDataException("Phiên bản bản nháp không được hỗ trợ.");
            var errors = new List<string>();
            var warnings = new List<string>();
            if (!string.Equals(doc.ProblemId, problem.Id, StringComparison.OrdinalIgnoreCase)) errors.Add("Bản đề đang gắn với khuôn input khác.");
            if (doc.Name.Trim().Length == 0) errors.Add("Tên bài đang để trống.");
            if (doc.Code.Trim().Length == 0) errors.Add("Mã bài đang để trống.");
            if (doc.Code.Length > 64 || doc.Code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
                errors.Add("Mã bài chỉ được gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới (tối đa 64 ký tự).");
            if (doc.TimeLimitMs < 1) errors.Add("Thời gian phải lớn hơn 0 ms.");
            if (doc.MemoryLimitMb < 1) errors.Add("Bộ nhớ phải lớn hơn 0 MB.");
            if (doc.Description.Trim().Length == 0 || doc.Description.Trim().Equals("Mô tả ý tưởng và yêu cầu của bài toán tại đây.", StringComparison.OrdinalIgnoreCase))
                errors.Add("Phần đề bài chưa được điền.");
            if (doc.Input.Trim().Length == 0) errors.Add("Chưa mô tả input.");
            if (doc.Output.Trim().Length == 0 || doc.Output.Trim().Equals("Mô tả định dạng output tại đây.", StringComparison.OrdinalIgnoreCase))
                errors.Add("Chưa mô tả output.");
            if (doc.Constraints.Trim().Length == 0 || doc.Constraints.Trim().Equals("Điền các giới hạn chính thức của đề bài tại đây.", StringComparison.OrdinalIgnoreCase))
                errors.Add("Chưa ghi giới hạn chính thức.");
            if (doc.Input.Trim() != problem.FormatDescription.TrimEnd()) warnings.Add("Mô tả input khác cấu trúc khuôn generator; cần đối chiếu thủ công.");
            if (doc.Examples.Trim().Length == 0 || doc.Examples.Trim().StartsWith("<!-- Dùng nút Chèn sample", StringComparison.OrdinalIgnoreCase))
                warnings.Add("Phần Ví dụ chưa có sample được chèn.");
            foreach (string error in errors) report.Add(CliAuditLevel.Error, "Nội dung đề", error);
            foreach (string warning in warnings) report.Add(CliAuditLevel.Warning, "Nội dung đề", warning);
            if (errors.Count == 0 && warnings.Count == 0) report.Add(CliAuditLevel.Pass, "Nội dung đề", "Các mục bắt buộc đã có và định dạng input khớp khuôn.");
        }
        catch (Exception ex) { report.Add(CliAuditLevel.Error, "Nội dung đề", "Không đọc/kiểm tra được bản nháp: " + ex.Message); }
    }

    private static void AuditReproducibility(IReadOnlyList<string> originalInputs, JobSpec? spec,
        Settings settings, IJobLog log, CliAuditReport report, CancellationToken ct)
    {
        if (spec?.Seed is not ulong seed)
        {
            report.Add(CliAuditLevel.Info, "Tái lập bộ test", "Không có seed trong lệnh hoặc hồ sơ; đã bỏ qua sinh lại để so sánh.");
            return;
        }
        string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OjGen-Cli-Reproducibility"));
        string tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            int generatedCount = spec.GeneratedTestCount > 0 ? spec.GeneratedTestCount : spec.TestCount;
            var regenerated = Generator.GenerateInputs(new GenerateRequest
            {
                Problem = spec.Problem,
                Cfg = spec.Cfg.Clone(),
                TestCount = generatedCount,
                Seed = seed,
                OutDir = tempDir,
                Prefix = settings.Prefix,
                Groups = spec.Groups,
                ManualTests = spec.ManualTests,
            }, log, ct);
            if (regenerated.Cancelled) { report.Cancelled = true; return; }
            if (!regenerated.Ok) { report.Add(CliAuditLevel.Error, "Tái lập bộ test", "Không sinh lại được test: " + regenerated.Error); return; }
            var freshInputs = FileManager.ListFilesWithExt(tempDir, ".inp");
            if (freshInputs.Count != originalInputs.Count)
            {
                report.Add(CliAuditLevel.Error, "Tái lập bộ test", $"Sinh lại được {freshInputs.Count} input nhưng thư mục có {originalInputs.Count}.");
                return;
            }
            var different = new List<string>();
            for (int i = 0; i < originalInputs.Count; i++)
            {
                if (ct.IsCancellationRequested) { report.Cancelled = true; return; }
                if (!string.Equals(HashFile(originalInputs[i]), HashFile(freshInputs[i]), StringComparison.OrdinalIgnoreCase))
                    different.Add(Path.GetFileName(originalInputs[i]));
            }
            if (different.Count == 0) report.Add(CliAuditLevel.Pass, "Tái lập bộ test", $"SHA-256 khớp cho cả {originalInputs.Count} input với seed {Util.LL(seed)}.");
            else report.Add(CliAuditLevel.Error, "Tái lập bộ test", $"{different.Count} input khác sau khi sinh lại: {string.Join(", ", different.Take(10))}.");
        }
        catch (Exception ex) { report.Add(CliAuditLevel.Error, "Tái lập bộ test", "Lỗi khi sinh lại test: " + ex.Message); }
        finally { DeleteTempChild(tempRoot, tempDir); }
    }

    private static void DeleteTempChild(string root, string target)
    {
        try
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullTarget = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetDirectoryName(fullTarget), fullRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullTarget))
                Directory.Delete(fullTarget, recursive: true);
        }
        catch (Exception) { }
    }

    private static void RunValidator(string path, IReadOnlyList<string> inputs, Problem problem, Config config,
        JobSpec? generatedSpec, Settings settings, IJobLog log, CliAuditReport report, CancellationToken ct)
    {
        string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OjGen-Cli-ReleaseAudit"));
        string tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var prepared = SolutionRunner.Prepare(path, settings, Path.Combine(tempDir, "build"), false, log, ct);
            if (!prepared.Ok) { report.Add(CliAuditLevel.Error, "Validator", prepared.Error); return; }
            var limits = Limits(settings);
            string stdout = Path.Combine(tempDir, "validator.stdout");
            string stderr = Path.Combine(tempDir, "validator.stderr");
            for (int i = 0; i < inputs.Count; i++)
            {
                if (ct.IsCancellationRequested) { report.Cancelled = true; return; }
                string input = Path.GetFullPath(inputs[i]);
                var argv = prepared.Argv.Concat(new[] { input }).ToList();
                RunResult valid = ProcessRunner.Run(argv, input, stdout, stderr, tempDir, limits, ct);
                if (valid.Status != RunStatus.Ok)
                {
                    report.Add(CliAuditLevel.Error, "Validator từ chối input hợp lệ", $"{Path.GetFileName(input)}: {ProcessRunner.StatusName(valid.Status)} — {valid.Message}");
                    return;
                }
                log.Progress("Kiểm tra validator · input hợp lệ", i + 1, inputs.Count);
            }
            string firstInput = FileManager.ReadText(inputs[0]);
            var invalid = InvalidVariants(firstInput, problem, ConfigFor(config, generatedSpec, 0));
            if (invalid.Count == 0)
            {
                report.Add(CliAuditLevel.Warning, "Validator hai chiều", "Không tạo được input sai mà khuôn xác nhận; chỉ kiểm tra input hợp lệ.");
                report.Add(CliAuditLevel.Pass, "Validator", $"Chấp nhận đủ {inputs.Count} input hợp lệ.");
                return;
            }
            for (int i = 0; i < invalid.Count; i++)
            {
                if (ct.IsCancellationRequested) { report.Cancelled = true; return; }
                string invalidPath = Path.Combine(tempDir, "invalid_" + (i + 1) + ".inp");
                File.WriteAllText(invalidPath, invalid[i], new UTF8Encoding(false));
                RunResult invalidRun = ProcessRunner.Run(prepared.Argv.Concat(new[] { invalidPath }).ToList(), invalidPath,
                    stdout, stderr, tempDir, limits, ct);
                if (invalidRun.Status == RunStatus.Ok)
                {
                    report.Add(CliAuditLevel.Error, "Validator chấp nhận input sai", $"Biến thể sai #{i + 1} được validator chấp nhận.");
                    return;
                }
                if (invalidRun.Status != RunStatus.RuntimeError || invalidRun.ExitCode == 0)
                {
                    report.Add(CliAuditLevel.Error, "Validator không từ chối gọn input sai", $"Biến thể #{i + 1}: {ProcessRunner.StatusName(invalidRun.Status)} — {invalidRun.Message}");
                    return;
                }
            }
            report.Add(CliAuditLevel.Pass, "Validator hai chiều", $"Chấp nhận đủ {inputs.Count} input hợp lệ và từ chối {invalid.Count} biến thể sai.");
        }
        catch (Exception ex) { report.Add(CliAuditLevel.Error, "Validator", "Lỗi khi chạy validator: " + ex.Message); }
        finally { DeleteTempChild(tempRoot, tempDir); }
    }

    private static void AuditPackageTools(CliAuditOptions options, IReadOnlyList<string> inputs,
        IJobLog log, CliAuditReport report, CancellationToken ct)
    {
        if (options.ValidatorPath.Trim().Length > 0)
        {
            if (inputs.Count == 0) report.Add(CliAuditLevel.Warning, "Validator", "Không có input để chạy validator.");
            else RunValidator(options.ValidatorPath, inputs, options.Problem, options.Config, options.GeneratedSpec,
                options.Settings, log, report, ct);
        }
        else report.Add(CliAuditLevel.Info, "Validator", "Chưa chọn validator; bước kiểm tra hai chiều được bỏ qua.");
        if (report.Cancelled) return;

        if (options.CheckerPath.Trim().Length > 0)
        {
            if (!File.Exists(options.CheckerPath)) report.Add(CliAuditLevel.Error, "Checker", "Không tìm thấy file checker đã chọn.");
            else report.Add(CliAuditLevel.Info, "Checker", "Đã xác nhận file checker; không chạy tự động vì giao diện đối số tùy judge.");
        }

        if (options.CandidateSolution.Trim().Length == 0)
        {
            report.Add(CliAuditLevel.Info, "Đối chiếu lời giải", "Chưa chọn lời giải thứ hai; kiểm thử độc lập được bỏ qua.");
            return;
        }
        if (options.ReferenceSolution.Trim().Length == 0 || !File.Exists(options.ReferenceSolution))
        {
            report.Add(CliAuditLevel.Error, "Đối chiếu lời giải", "Cần file --solution hợp lệ làm lời giải chuẩn.");
            return;
        }
        if (!File.Exists(options.CandidateSolution))
        {
            report.Add(CliAuditLevel.Error, "Đối chiếu lời giải", "Không tìm thấy lời giải độc lập đã chọn.");
            return;
        }
        if (string.Equals(Path.GetFullPath(options.ReferenceSolution), Path.GetFullPath(options.CandidateSolution), StringComparison.OrdinalIgnoreCase))
        {
            report.Add(CliAuditLevel.Error, "Đối chiếu lời giải", "Lời giải độc lập phải là file khác lời giải chuẩn.");
            return;
        }
        ulong seed = options.DifferentialSeed ?? Generator.RandomSeed();
        var diff = DifferentialTester.Run(new DifferentialSpec
        {
            Problem = options.Problem,
            Cfg = DifferentialTester.CreateSmallConfig(options.Problem, options.Config),
            TestCount = 100,
            Seed = seed,
            ReferencePath = options.ReferenceSolution,
            CandidatePath = options.CandidateSolution,
            IgnoreWhitespace = true,
        }, options.Settings, log, ct);
        if (diff.Cancelled) { report.Cancelled = true; return; }
        if (diff.Ok) report.Add(CliAuditLevel.Pass, "Đối chiếu lời giải", $"Hai lời giải trùng output trên {diff.Tested} test nhỏ (seed {Util.LL(seed)}).");
        else
        {
            string details = diff.Error.Length > 0 ? diff.Error : $"{diff.Mismatches} khác biệt trên {diff.Tested} test.";
            foreach (var failure in diff.Failures.Take(5)) details += $"\nTest {failure.TestIndex}: {failure.Reason}";
            report.Add(CliAuditLevel.Error, "Đối chiếu lời giải", details);
        }
    }

    private static void WriteReleaseFiles(CliAuditOptions options, CliAuditReport report, ulong? actualSeed)
    {
        Directory.CreateDirectory(options.TestDirectory);
        if (options.StatementPath.Trim().Length > 0 && File.Exists(options.StatementPath))
            File.Copy(options.StatementPath, Path.Combine(options.TestDirectory, "problem.ojdraft.json"), overwrite: true);
        File.WriteAllText(Path.Combine(options.TestDirectory, "AUDIT_REPORT.txt"), report.ToText(), new UTF8Encoding(false));

        if (!actualSeed.HasValue)
        {
            string infoPath = Path.Combine(options.TestDirectory, FileManager.InfoFile);
            if (File.Exists(infoPath))
            {
                foreach (string line in File.ReadLines(infoPath))
                {
                    int colon = line.IndexOf(':');
                    if (colon < 0 || !line[..colon].Trim().Equals("Seed", StringComparison.OrdinalIgnoreCase)) continue;
                    if (Util.ParseULL(line[(colon + 1)..].Trim(), out ulong recorded)) actualSeed = recorded;
                    break;
                }
            }
        }

        string OptionalHash(string path) => path.Trim().Length == 0 ? "not recorded" : File.Exists(path) ? HashFile(path) : "missing";
        string packagePath = Path.Combine(options.TestDirectory, "PACKAGE.txt");
        string PackageText(int count) => new StringBuilder()
            .Append("Input generator: ").Append(options.Problem.Id).Append('\n')
            .Append("OjGen version: ").Append(Util.Version).Append('\n')
            .Append("Runtime: ").Append(RuntimeInformation.FrameworkDescription).Append('\n')
            .Append("OS: ").Append(RuntimeInformation.OSDescription).Append('\n')
            .Append("Test seed: ").Append(actualSeed.HasValue ? Util.LL(actualSeed.Value) : "not recorded").Append('\n')
            .Append("Reference solution SHA-256: ").Append(OptionalHash(options.ReferenceSolution)).Append('\n')
            .Append("Validator SHA-256: ").Append(OptionalHash(options.ValidatorPath)).Append('\n')
            .Append("Checker SHA-256: ").Append(OptionalHash(options.CheckerPath)).Append('\n')
            .Append("Independent solution SHA-256: ").Append(OptionalHash(options.CandidateSolution)).Append('\n')
            .Append("Tests: ").Append(FileManager.ListFilesWithExt(options.TestDirectory, ".inp").Count).Append(" input\n")
            .Append("Package checksum manifest: ").Append(PackageChecksumFile).Append(" (").Append(count).Append(" files, excludes itself)\n")
            .ToString();

        string checksumPath = Path.Combine(options.TestDirectory, PackageChecksumFile);
        List<string> BundleFiles()
        {
            var files = FileManager.ListFilesWithExt(options.TestDirectory, ".inp");
            files.AddRange(FileManager.ListFilesWithExt(options.TestDirectory, ".out"));
            foreach (string name in new[] { FileManager.InfoFile, FileManager.ChecksumFile, "AUDIT_REPORT.txt", "PACKAGE.txt", "problem.ojdraft.json" })
            {
                string path = Path.Combine(options.TestDirectory, name);
                if (File.Exists(path)) files.Add(path);
            }
            return files.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToList();
        }
        File.WriteAllText(packagePath, PackageText(BundleFiles().Count), new UTF8Encoding(false));
        for (int pass = 0; pass < 2; pass++)
        {
            var lines = new StringBuilder();
            foreach (string file in BundleFiles()) lines.Append(HashFile(file)).Append("  ").Append(Path.GetFileName(file)).Append('\n');
            File.WriteAllText(checksumPath, lines.ToString(), new UTF8Encoding(false));
            File.WriteAllText(packagePath, PackageText(BundleFiles().Count), new UTF8Encoding(false));
        }
        var finalLines = new StringBuilder();
        foreach (string file in BundleFiles()) finalLines.Append(HashFile(file)).Append("  ").Append(Path.GetFileName(file)).Append('\n');
        File.WriteAllText(checksumPath, finalLines.ToString(), new UTF8Encoding(false));
    }

    public static CliAuditReport Run(CliAuditOptions options, IJobLog log, CancellationToken ct = default)
    {
        var report = new CliAuditReport();
        try
        {
            AuditStatement(options.StatementPath, options.Problem, report);
            if (!Directory.Exists(options.TestDirectory))
            {
                report.Add(CliAuditLevel.Error, "Bộ test", "Không tìm thấy thư mục: " + options.TestDirectory);
            }
            else
            {
                var inputs = FileManager.ListFilesWithExt(options.TestDirectory, ".inp");
                if (inputs.Count == 0) report.Add(CliAuditLevel.Error, "Bộ test", "Không tìm thấy file .inp.");
                else
                {
                    report.Add(CliAuditLevel.Pass, "Nguồn test", $"Tìm thấy {inputs.Count} input trong {options.TestDirectory}.");
                    if (options.ExpectedTestCount > 0 && inputs.Count != options.ExpectedTestCount)
                        report.Add(CliAuditLevel.Error, "Số lượng test", $"Mong đợi {options.ExpectedTestCount}, nhưng thư mục có {inputs.Count} input.");

                    int invalid = 0, missing = 0, duplicate = 0, orphan = 0;
                    var expectedOutputs = inputs.Select(path => Path.GetFileName(Path.ChangeExtension(path, ".out")))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (string output in FileManager.ListFilesWithExt(options.TestDirectory, ".out"))
                    {
                        if (expectedOutputs.Contains(Path.GetFileName(output))) continue;
                        orphan++;
                        if (orphan <= 10) report.Add(CliAuditLevel.Error, "Output mồ côi", Path.GetFileName(output) + " không có input cùng tên.");
                    }
                    var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < inputs.Count; i++)
                    {
                        if (ct.IsCancellationRequested) { report.Cancelled = true; break; }
                        string input = inputs[i];
                        string text = FileManager.ReadText(input);
                        if (options.Problem.ValidateInput(text, ConfigFor(options.Config, options.GeneratedSpec, i)) is { } error)
                        {
                            invalid++;
                            if (invalid <= 10) report.Add(CliAuditLevel.Error, "Input không hợp lệ", $"{Path.GetFileName(input)}: {error}");
                        }
                        string hash = HashFile(input);
                        if (hashes.TryGetValue(hash, out string? prior))
                        {
                            duplicate++;
                            if (duplicate <= 10) report.Add(CliAuditLevel.Warning, "Test trùng lặp", $"{Path.GetFileName(input)} trùng input với {prior}.");
                        }
                        else hashes.Add(hash, Path.GetFileName(input));
                        if (!File.Exists(Path.ChangeExtension(input, ".out")))
                        {
                            missing++;
                            if (missing <= 10) report.Add(CliAuditLevel.Error, "Thiếu output", $"Không có file .out cho {Path.GetFileName(input)}.");
                        }
                        log.Progress("Kiểm tra test phát hành", i + 1, inputs.Count);
                    }
                    if (invalid == 0) report.Add(CliAuditLevel.Pass, "Định dạng input", $"Tất cả {inputs.Count} input hợp lệ theo khuôn và cấu hình.");
                    else if (invalid > 10) report.Add(CliAuditLevel.Error, "Định dạng input", $"Còn {invalid - 10} input lỗi không hiển thị.");
                    if (missing == 0) report.Add(CliAuditLevel.Pass, "Output", $"Đủ output cho cả {inputs.Count} test.");
                    else if (missing > 10) report.Add(CliAuditLevel.Error, "Output", $"Còn {missing - 10} output bị thiếu không hiển thị.");
                    if (duplicate > 10) report.Add(CliAuditLevel.Warning, "Test trùng lặp", $"Còn {duplicate - 10} test trùng không hiển thị.");
                    if (orphan > 10) report.Add(CliAuditLevel.Error, "Output mồ côi", $"Còn {orphan - 10} output không có input không hiển thị.");

                    IReadOnlyList<TestGroupSpec> groups = options.GeneratedSpec?.Groups.Count > 0 ? options.GeneratedSpec.Groups : new[]
                    {
                        new TestGroupSpec { Name = "Tất cả", TestCount = inputs.Count, Cfg = options.Config.Clone() },
                    };
                    var coverage = CoverageAnalyzer.Analyze(options.Problem, inputs, groups, options.GeneratedSpec?.ManualTests ?? new List<ManualTestSpec>());
                    if (coverage.Suggestions.Count == 0) report.Add(CliAuditLevel.Pass, "Độ phủ test (heuristic)", "Không thấy thiếu chế độ rõ ràng từ tên nhóm và cấu hình.");
                    else
                    {
                        foreach (string suggestion in coverage.Suggestions.Take(6)) report.Add(CliAuditLevel.Warning, "Độ phủ test (heuristic)", suggestion);
                        report.Add(CliAuditLevel.Info, "Độ phủ test", "Các gợi ý heuristic cần được so với trường hợp biên thực tế của đề.");
                    }

                    AuditReproducibility(inputs, options.GeneratedSpec, options.Settings, log, report, ct);
                    if (!File.Exists(Path.Combine(options.TestDirectory, FileManager.InfoFile)))
                        report.Add(CliAuditLevel.Warning, "Nguồn gốc test", $"Thiếu {FileManager.InfoFile}; không có bản ghi cấu hình đi kèm.");
                    string? checksumError = FileManager.VerifyTestChecksumManifest(options.TestDirectory, out int checksumCount);
                    if (checksumError == null) report.Add(CliAuditLevel.Pass, "Toàn vẹn test", $"SHA-256 khớp cho {checksumCount} file.");
                    else if (checksumError == "Chưa có manifest SHA-256.")
                    {
                        report.Add(CliAuditLevel.Warning, "Toàn vẹn test", "Chưa có manifest cũ; OjGen sẽ tạo SHA-256 mới cho gói này.");
                        if (!FileManager.WriteTestChecksumManifest(options.TestDirectory, out checksumCount, out string? writeError))
                            report.Add(CliAuditLevel.Error, "Toàn vẹn test", writeError ?? "Không thể ghi manifest SHA-256.");
                        else report.Add(CliAuditLevel.Pass, "Toàn vẹn test", $"Đã ghi SHA-256 cho {checksumCount} file.");
                    }
                    else report.Add(CliAuditLevel.Error, "Toàn vẹn test", checksumError);
                }
            }

            if (!report.Cancelled) AuditPackageTools(options, FileManager.ListFilesWithExt(options.TestDirectory, ".inp"), log, report, ct);
        }
        catch (Exception ex) { report.Add(CliAuditLevel.Error, "Kiểm định", "Lỗi khi kiểm định: " + ex.Message); }
        log.Line(report.ToText());
        try { WriteReleaseFiles(options, report, options.GeneratedSpec?.Seed ?? options.Seed); }
        catch (Exception ex) { report.Add(CliAuditLevel.Error, "Ghi gói phát hành", ex.Message); log.Line(report.ToText()); }
        return report;
    }
}
