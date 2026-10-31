using System.IO;
using System.Security.Cryptography;
using System.Text;
using OjGen.Core;

namespace OjGen.Gui;

internal enum ReleaseAuditLevel { Pass, Info, Warning, Error }

internal sealed record ReleaseAuditItem(ReleaseAuditLevel Level, string Title, string Detail);

internal sealed class ReleaseAuditReport
{
    public List<ReleaseAuditItem> Items { get; } = new();
    public bool Cancelled { get; set; }
    public bool HasErrors => Items.Any(item => item.Level == ReleaseAuditLevel.Error);
    public int ErrorCount => Items.Count(item => item.Level == ReleaseAuditLevel.Error);
    public int WarningCount => Items.Count(item => item.Level == ReleaseAuditLevel.Warning);

    public void Add(ReleaseAuditLevel level, string title, string detail) => Items.Add(new(level, title, detail));

    public string ToText()
    {
        string status = Cancelled ? "ĐÃ HỦY" : HasErrors ? "CHƯA ĐẠT" : WarningCount > 0 ? "ĐẠT CÓ LƯU Ý" : "ĐẠT";
        var sb = new StringBuilder().Append("KIỂM ĐỊNH PHÁT HÀNH: ").Append(status).Append("\n\n");
        foreach (var item in Items)
        {
            string icon = item.Level switch
            {
                ReleaseAuditLevel.Pass => "[ĐẠT]",
                ReleaseAuditLevel.Warning => "[LƯU Ý]",
                ReleaseAuditLevel.Error => "[LỖI]",
                _ => "[INFO]",
            };
            sb.Append(icon).Append(' ').Append(item.Title).Append('\n');
            if (item.Detail.Length > 0) sb.Append("      ").Append(item.Detail.Replace("\n", "\n      ")).Append('\n');
        }
        if (Cancelled) sb.Append("\nĐã hủy trong khi kiểm định; chưa thể đóng gói bản phát hành.\n");
        return sb.ToString();
    }
}

internal sealed class ReleaseAuditOptions
{
    public bool IncludeTests { get; set; }
    public string TestDirectory { get; set; } = "";
    public int ExpectedTestCount { get; set; }
    public string ReferenceSolution { get; set; } = "";
    public string ValidatorPath { get; set; } = "";
    public string CheckerPath { get; set; } = "";
    public string CandidateSolution { get; set; } = "";
    public ulong Seed { get; set; }
    public JobSpec? GeneratedSpec { get; set; }
}

internal static class ReleaseAuditService
{
    private static Config ConfigFor(Config fallback, JobSpec? spec, int testIndex)
    {
        if (spec == null) return fallback;
        if (testIndex < spec.ManualTests.Count) return spec.ManualTests[testIndex].Cfg;
        int generatedIndex = testIndex - spec.ManualTests.Count;
        if (spec.Groups.Count == 0) return spec.Cfg;
        foreach (var group in spec.Groups)
        {
            if (generatedIndex < group.TestCount) return group.Cfg;
            generatedIndex -= group.TestCount;
        }
        return fallback;
    }

    private static List<string> InvalidVariants(string input, Problem problem, Config config)
    {
        var candidates = new List<string>
        {
            "",
            input + "OJGEN_INVALID_TRAILING_TOKEN\n",
            "OJGEN_INVALID\n",
        };
        string trimmed = input.TrimEnd('\r', '\n');
        int lastWhitespace = trimmed.LastIndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        if (lastWhitespace >= 0)
            candidates.Add(trimmed[..(lastWhitespace + 1)].TrimEnd('\r', '\n') + "\n");

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

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void AuditReproducibility(IReadOnlyList<string> originalInputs, JobSpec? spec,
        Settings settings, IJobLog log, ReleaseAuditReport report, CancellationToken ct)
    {
        if (spec?.Seed is not ulong seed)
        {
            report.Add(ReleaseAuditLevel.Info, "Tái lập bộ test", "Không có hồ sơ sinh trong phiên này để sinh lại cùng seed.");
            return;
        }
        string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OjGen-Reproducibility"));
        string tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            int generatedCount = spec.GeneratedTestCount > 0
                ? spec.GeneratedTestCount
                : Math.Max(1, spec.TestCount - spec.ManualTests.Count);
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
            if (!regenerated.Ok)
            {
                report.Add(ReleaseAuditLevel.Error, "Tái lập bộ test", "Không sinh lại được test với hồ sơ đã lưu: " + regenerated.Error);
                return;
            }
            var freshInputs = FileManager.ListFilesWithExt(tempDir, ".inp");
            if (freshInputs.Count != originalInputs.Count)
            {
                report.Add(ReleaseAuditLevel.Error, "Tái lập bộ test", $"Sinh lại được {freshInputs.Count} input nhưng thư mục phát hành có {originalInputs.Count}.");
                return;
            }
            var differences = new List<string>();
            for (int i = 0; i < originalInputs.Count; i++)
            {
                if (ct.IsCancellationRequested) { report.Cancelled = true; return; }
                if (!string.Equals(HashFile(originalInputs[i]), HashFile(freshInputs[i]), StringComparison.OrdinalIgnoreCase))
                    differences.Add(Path.GetFileName(originalInputs[i]));
            }
            if (differences.Count == 0)
                report.Add(ReleaseAuditLevel.Pass, "Tái lập bộ test", $"Sinh lại đúng cả {originalInputs.Count} input bằng seed {Util.LL(seed)}; SHA-256 khớp.");
            else report.Add(ReleaseAuditLevel.Error, "Tái lập bộ test", $"{differences.Count} input khác sau khi sinh lại. Ví dụ: {string.Join(", ", differences.Take(10))}.");
        }
        catch (Exception ex)
        {
            report.Add(ReleaseAuditLevel.Error, "Tái lập bộ test", "Lỗi khi sinh lại test: " + ex.Message);
        }
        finally
        {
            try
            {
                string fullRoot = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullTarget = Path.GetFullPath(tempDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(Path.GetDirectoryName(fullTarget), fullRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullTarget))
                    Directory.Delete(fullTarget, recursive: true);
            }
            catch (Exception) { }
        }
    }

    public static ReleaseAuditReport Run(ReleaseAuditOptions options, ProblemAuthoringDocument document,
        Problem problem, Config config, IReadOnlyList<ManualTestSpec> manualTests,
        Settings settings, IJobLog log, CancellationToken ct = default)
    {
        var report = new ReleaseAuditReport();
        try
        {
            var statement = StatementConsistencyChecker.Check(document, problem, config,
                manualTests.Where(test => test.IsSample).ToList());
            foreach (string error in statement.Errors) report.Add(ReleaseAuditLevel.Error, "Nội dung đề", error);
            foreach (string warning in statement.Warnings) report.Add(ReleaseAuditLevel.Warning, "Nội dung đề", warning);
            if (statement.Errors.Count == 0 && statement.Warnings.Count == 0)
                report.Add(ReleaseAuditLevel.Pass, "Nội dung đề", "Các mục bắt buộc đã có và sample khớp lời giải chuẩn.");

            List<string> inputs = new();
            if (Directory.Exists(options.TestDirectory)) inputs = FileManager.ListFilesWithExt(options.TestDirectory, ".inp");
            if (!options.IncludeTests)
            {
                inputs.Clear();
                report.Add(ReleaseAuditLevel.Warning, "Bộ test", "Gói không kèm test; rà soát input, output và checksum của test được bỏ qua.");
            }
            else if (inputs.Count == 0)
            {
                report.Add(ReleaseAuditLevel.Error, "Bộ test", "Đã chọn kèm test nhưng không tìm thấy file .inp.");
            }
            else
            {
                if (options.IncludeTests) report.Add(ReleaseAuditLevel.Pass, "Nguồn test", $"Tìm thấy {inputs.Count} input trong {options.TestDirectory}.");
                else report.Add(ReleaseAuditLevel.Warning, "Nguồn test", "Gói phát hành sẽ không chứa các test này.");
                if (options.ExpectedTestCount > 0 && inputs.Count != options.ExpectedTestCount)
                    report.Add(ReleaseAuditLevel.Error, "Số lượng test", $"Mong đợi {options.ExpectedTestCount}, nhưng thư mục có {inputs.Count} input.");

                int invalidInputs = 0, missingOutputs = 0, duplicateInputs = 0, orphanOutputs = 0;
                var expectedOutputNames = inputs.Select(path => Path.ChangeExtension(path, ".out")).Select(path => Path.GetFileName(path))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string output in FileManager.ListFilesWithExt(options.TestDirectory, ".out"))
                {
                    if (expectedOutputNames.Contains(Path.GetFileName(output))) continue;
                    orphanOutputs++;
                    if (orphanOutputs <= 10)
                        report.Add(ReleaseAuditLevel.Error, "Output mồ côi", $"{Path.GetFileName(output)} không có input cùng tên.");
                }
                var inputHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < inputs.Count; i++)
                {
                    if (ct.IsCancellationRequested) { report.Cancelled = true; return report; }
                    string input = inputs[i];
                    string text = FileManager.ReadText(input);
                    Config testConfig = ConfigFor(config, options.GeneratedSpec, i);
                    if (problem.ValidateInput(text, testConfig) is { } validationError)
                    {
                        invalidInputs++;
                        if (invalidInputs <= 10) report.Add(ReleaseAuditLevel.Error, "Input không hợp lệ", $"{Path.GetFileName(input)}: {validationError}");
                    }
                    string hash = HashFile(input);
                    if (inputHashes.TryGetValue(hash, out string? prior))
                    {
                        duplicateInputs++;
                        if (duplicateInputs <= 10)
                            report.Add(ReleaseAuditLevel.Warning, "Test trùng lặp", $"{Path.GetFileName(input)} có cùng input với {prior}.");
                    }
                    else inputHashes.Add(hash, Path.GetFileName(input));

                    if (!File.Exists(Path.ChangeExtension(input, ".out")))
                    {
                        missingOutputs++;
                        if (missingOutputs <= 10) report.Add(ReleaseAuditLevel.Error, "Thiếu output", $"Không có file .out cho {Path.GetFileName(input)}.");
                    }
                    log.Progress("Kiểm tra test phát hành", i + 1, inputs.Count);
                }
                if (invalidInputs == 0) report.Add(ReleaseAuditLevel.Pass, "Định dạng input", $"Tất cả {inputs.Count} input hợp lệ theo khuôn và cấu hình.");
                else if (invalidInputs > 10) report.Add(ReleaseAuditLevel.Error, "Định dạng input", $"Còn {invalidInputs - 10} input lỗi không hiển thị.");
                if (missingOutputs == 0) report.Add(ReleaseAuditLevel.Pass, "Output", $"Đủ output cho cả {inputs.Count} test.");
                else if (missingOutputs > 10) report.Add(ReleaseAuditLevel.Error, "Output", $"Còn {missingOutputs - 10} output bị thiếu không hiển thị.");
                if (duplicateInputs > 10) report.Add(ReleaseAuditLevel.Warning, "Test trùng lặp", $"Còn {duplicateInputs - 10} test trùng không hiển thị.");
                if (orphanOutputs > 10) report.Add(ReleaseAuditLevel.Error, "Output mồ côi", $"Còn {orphanOutputs - 10} output không có input không hiển thị.");

                var coverageSpec = options.GeneratedSpec;
                IReadOnlyList<TestGroupSpec> coverageGroups = coverageSpec?.Groups.Count > 0
                    ? coverageSpec.Groups
                    : new[]
                    {
                        new TestGroupSpec
                        {
                            Name = "Tất cả",
                            TestCount = Math.Max(1, coverageSpec?.GeneratedTestCount ?? inputs.Count),
                            Cfg = coverageSpec?.Cfg.Clone() ?? config.Clone(),
                        },
                    };
                IReadOnlyList<ManualTestSpec> coverageManual = coverageSpec?.ManualTests ?? manualTests.ToList();
                var coverage = CoverageAnalyzer.Analyze(problem, inputs, coverageGroups, coverageManual);
                if (coverage.Suggestions.Count == 0)
                    report.Add(ReleaseAuditLevel.Pass, "Độ phủ test (heuristic)", "Không thấy thiếu chế độ rõ ràng từ tên nhóm và cấu hình.");
                else
                {
                    foreach (string suggestion in coverage.Suggestions.Take(6))
                        report.Add(ReleaseAuditLevel.Warning, "Độ phủ test (heuristic)", suggestion);
                    report.Add(ReleaseAuditLevel.Info, "Độ phủ test", "Các gợi ý suy ra từ cấu hình/tên nhóm; cần so với các trường hợp biên thực tế của đề.");
                }

                if (options.GeneratedSpec?.Problem.Id == problem.Id &&
                    string.Equals(Path.GetFullPath(options.GeneratedSpec.OutDir), Path.GetFullPath(options.TestDirectory), StringComparison.OrdinalIgnoreCase))
                    AuditReproducibility(inputs, options.GeneratedSpec, settings, log, report, ct);
                else report.Add(ReleaseAuditLevel.Info, "Tái lập bộ test", "Không có hồ sơ sinh khớp với thư mục này; đã bỏ qua so sánh tái lập.");
                if (report.Cancelled) return report;

                if (!File.Exists(Path.Combine(options.TestDirectory, FileManager.InfoFile)))
                    report.Add(ReleaseAuditLevel.Warning, "Nguồn gốc test", $"Thiếu {FileManager.InfoFile}; không có bản ghi seed/cấu hình đi kèm.");
                string? checksumError = FileManager.VerifyTestChecksumManifest(options.TestDirectory, out int checksumCount);
                if (checksumError == null) report.Add(ReleaseAuditLevel.Pass, "Toàn vẹn test", $"Đã xác minh SHA-256 của {checksumCount} file.");
                else if (checksumError == "Chưa có manifest SHA-256.") report.Add(ReleaseAuditLevel.Warning, "Toàn vẹn test", "Thư mục được tạo trước khi có manifest; sẽ ghi dấu SHA-256 mới vào gói phát hành.");
                else report.Add(ReleaseAuditLevel.Error, "Toàn vẹn test", checksumError);
            }

            if (options.ValidatorPath.Trim().Length > 0)
            {
                if (inputs.Count == 0)
                    report.Add(ReleaseAuditLevel.Warning, "Validator", "Không thể chạy validator vì chưa có input.");
                else RunValidator(options.ValidatorPath, inputs, problem, config, options.GeneratedSpec,
                    settings, log, report, ct);
                if (report.Cancelled) return report;
            }
            else report.Add(ReleaseAuditLevel.Info, "Validator", "Chưa chọn validator; bước kiểm tra hai chiều được bỏ qua.");

            if (options.CheckerPath.Trim().Length > 0)
            {
                if (!File.Exists(options.CheckerPath)) report.Add(ReleaseAuditLevel.Error, "Checker", "Không tìm thấy file checker đã chọn.");
                else report.Add(ReleaseAuditLevel.Info, "Checker", "Đã xác nhận file tồn tại; checker không được chạy tự động vì giao diện đối số khác nhau giữa các judge.");
            }

            if (options.CandidateSolution.Trim().Length > 0)
            {
                if (options.ReferenceSolution.Trim().Length == 0 || !File.Exists(options.ReferenceSolution))
                    report.Add(ReleaseAuditLevel.Error, "Đối chiếu lời giải", "Cần có file lời giải chuẩn hợp lệ để đối chiếu.");
                else if (!File.Exists(options.CandidateSolution))
                    report.Add(ReleaseAuditLevel.Error, "Đối chiếu lời giải", "Không tìm thấy lời giải độc lập đã chọn.");
                else if (string.Equals(Path.GetFullPath(options.ReferenceSolution), Path.GetFullPath(options.CandidateSolution), StringComparison.OrdinalIgnoreCase))
                    report.Add(ReleaseAuditLevel.Error, "Đối chiếu lời giải", "Lời giải độc lập phải là file khác lời giải chuẩn.");
                else
                {
                    var diff = DifferentialTester.Run(new DifferentialSpec
                    {
                        Problem = problem,
                        Cfg = DifferentialTester.CreateSmallConfig(problem, config),
                        TestCount = 100,
                        Seed = options.Seed,
                        ReferencePath = options.ReferenceSolution,
                        CandidatePath = options.CandidateSolution,
                        IgnoreWhitespace = true,
                    }, settings, log, ct);
                    if (diff.Cancelled) { report.Cancelled = true; return report; }
                    if (diff.Ok) report.Add(ReleaseAuditLevel.Pass, "Đối chiếu lời giải", $"Hai lời giải cho cùng output trên {diff.Tested} test nhỏ (seed {Util.LL(options.Seed)}).");
                    else
                    {
                        string details = diff.Error.Length > 0 ? diff.Error : $"{diff.Mismatches} khác biệt trên {diff.Tested} test.";
                        foreach (var failure in diff.Failures.Take(5)) details += $"\nTest {failure.TestIndex}: {failure.Reason}";
                        report.Add(ReleaseAuditLevel.Error, "Đối chiếu lời giải", details);
                    }
                }
            }
            else report.Add(ReleaseAuditLevel.Info, "Đối chiếu lời giải", "Chưa chọn lời giải thứ hai; kiểm thử độc lập được bỏ qua.");
        }
        catch (Exception ex)
        {
            report.Add(ReleaseAuditLevel.Error, "Kiểm định", "Lỗi khi kiểm định: " + ex.Message);
        }
        log.Line(report.ToText());
        return report;
    }

    private static void RunValidator(string path, IReadOnlyList<string> inputs, Problem problem, Config config,
        JobSpec? generatedSpec, Settings settings, IJobLog log, ReleaseAuditReport report, CancellationToken ct)
    {
        string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OjGen-ReleaseAudit"));
        string tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var prepared = SolutionRunner.Prepare(path, settings, Path.Combine(tempDir, "build"), false, log, ct);
            if (!prepared.Ok)
            {
                report.Add(ReleaseAuditLevel.Error, "Validator", prepared.Error);
                return;
            }
            var limits = Limits(settings);
            string stdout = Path.Combine(tempDir, "validator.stdout");
            string stderr = Path.Combine(tempDir, "validator.stderr");
            int rejected = 0;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (ct.IsCancellationRequested) { report.Cancelled = true; return; }
                var argv = prepared.Argv.Concat(new[] { Path.GetFullPath(inputs[i]) }).ToList();
                RunResult valid = ProcessRunner.Run(argv, Path.GetFullPath(inputs[i]), stdout, stderr, tempDir, limits, ct);
                if (valid.Status != RunStatus.Ok)
                {
                    report.Add(ReleaseAuditLevel.Error, "Validator từ chối input hợp lệ", $"{Path.GetFileName(inputs[i])}: {ProcessRunner.StatusName(valid.Status)} — {valid.Message}");
                    return;
                }
                log.Progress("Kiểm tra validator · input hợp lệ", i + 1, inputs.Count);
            }

            string firstInput = FileManager.ReadText(inputs[0]);
            Config firstConfig = ConfigFor(config, generatedSpec, 0);
            var invalid = InvalidVariants(firstInput, problem, firstConfig);
            if (invalid.Count == 0)
            {
                report.Add(ReleaseAuditLevel.Warning, "Validator hai chiều", "Không tạo được biến thể sai mà khuôn hiện có xác nhận; chỉ kiểm tra input hợp lệ.");
                report.Add(ReleaseAuditLevel.Pass, "Validator", $"Chấp nhận đủ {inputs.Count} input hợp lệ.");
                return;
            }
            for (int i = 0; i < invalid.Count; i++)
            {
                if (ct.IsCancellationRequested) { report.Cancelled = true; return; }
                string invalidPath = Path.Combine(tempDir, "invalid_" + (i + 1) + ".inp");
                File.WriteAllText(invalidPath, invalid[i], new UTF8Encoding(false));
                var argv = prepared.Argv.Concat(new[] { invalidPath }).ToList();
                RunResult invalidRun = ProcessRunner.Run(argv, invalidPath, stdout, stderr, tempDir, limits, ct);
                if (invalidRun.Status == RunStatus.Ok)
                {
                    report.Add(ReleaseAuditLevel.Error, "Validator chấp nhận input sai", $"Biến thể sai #{i + 1} được validator chấp nhận (mã thoát 0).");
                    return;
                }
                if (invalidRun.Status != RunStatus.RuntimeError || invalidRun.ExitCode == 0)
                {
                    report.Add(ReleaseAuditLevel.Error, "Validator không từ chối gọn input sai", $"Biến thể #{i + 1}: {ProcessRunner.StatusName(invalidRun.Status)} — {invalidRun.Message}");
                    return;
                }
                rejected++;
            }
            report.Add(ReleaseAuditLevel.Pass, "Validator hai chiều", $"Chấp nhận đủ {inputs.Count} input hợp lệ và từ chối {rejected} biến thể sai.");
        }
        catch (Exception ex)
        {
            report.Add(ReleaseAuditLevel.Error, "Validator", "Lỗi khi chạy validator: " + ex.Message);
        }
        finally
        {
            try
            {
                string fullRoot = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullTarget = Path.GetFullPath(tempDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(Path.GetDirectoryName(fullTarget), fullRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullTarget))
                    Directory.Delete(fullTarget, recursive: true);
            }
            catch (Exception) { }
        }
    }
}
