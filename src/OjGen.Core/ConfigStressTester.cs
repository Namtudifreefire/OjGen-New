using System.Text;
using System.Text.Json;

namespace OjGen.Core;

/// <summary>Kết quả một lần chạy stress test cho một cấu hình.</summary>
public sealed class StressTestResult
{
    public string ConfigName { get; set; } = "";
    public Dictionary<string, long> ConfigValues { get; set; } = new();
    public int TestCount { get; set; }
    public int OkCount { get; set; }
    public int TleCount { get; set; }
    public int MleCount { get; set; }
    public int ReCount { get; set; }
    public int WaCount { get; set; }
    public long MaxTimeMs { get; set; }
    public long MedianTimeMs { get; set; }
    public long P95TimeMs { get; set; }
    public long PeakMemoryBytes { get; set; }
    public string Status => WaCount > 0 ? "WA" : TleCount > 0 ? "TLE" : MleCount > 0 ? "MLE" : ReCount > 0 ? "RE" : "OK";
    public double PassRate => TestCount > 0 ? (double)OkCount / TestCount * 100 : 0;
}

/// <summary>Cấu hình cho stress test tham số.</summary>
public sealed class StressTestSpec
{
    public Problem Problem { get; set; } = null!;
    public Config BaseConfig { get; set; } = new();
    public string SolutionPath { get; set; } = "";
    public string? ReferencePath { get; set; } // Optional: for WA detection
    public List<ParameterRange> ParameterRanges { get; set; } = new();
    public int TestsPerConfig { get; set; } = 10;
    public RunLimits Limits { get; set; } = new();
    public string? WorkingDirectory { get; set; }
    public bool IgnoreWhitespace { get; set; } = true;
    public ulong? BaseSeed { get; set; } = null; // null = random each config
    public int MaxParallel { get; set; } = 1; // Future: parallel execution
}

/// <summary>Khoảng giá trị cho một tham số.</summary>
public sealed class ParameterRange
{
    public string Key { get; set; } = "";
    public long Min { get; set; }
    public long Max { get; set; }
    public long Step { get; set; } = 1;
    public List<long>? Values { get; set; } // Explicit values instead of range
}

/// <summary>Báo cáo stress test tổng thể.</summary>
public sealed class StressTestReport
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public List<StressTestResult> Results { get; set; } = new();
    public TimeSpan TotalTime { get; set; }
    public int TotalConfigsTested { get; set; }
    public int TotalTestsRun { get; set; }

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== BÁO CÁO STRESS TEST ===");
        sb.AppendLine($"Tổng cấu hình test: {TotalConfigsTested}");
        sb.AppendLine($"Tổng test chạy: {TotalTestsRun}");
        sb.AppendLine($"Thời gian: {TotalTime.TotalSeconds:F1}s");
        sb.AppendLine("");

        // Summary table
        sb.AppendLine("CẤU HÌNH                    | TEST | OK  | TLE | MLE | RE  | WA  | PASS% | MAX(ms) | MED(ms) | P95(ms) | MEM(MB)");
        sb.AppendLine(new string('-', 110));

        foreach (var r in Results)
        {
            var configStr = string.Join(", ", r.ConfigValues.Select(kv => $"{kv.Key}={kv.Value}"));
            if (configStr.Length > 26) configStr = configStr[..23] + "...";

            sb.AppendLine($"{configStr,-26} | {r.TestCount,4} | {r.OkCount,3} | {r.TleCount,3} | {r.MleCount,3} | {r.ReCount,3} | {r.WaCount,3} | {r.PassRate,5:F1} | {r.MaxTimeMs,7} | {r.MedianTimeMs,7} | {r.P95TimeMs,7} | {r.PeakMemoryBytes / (1024*1024),7}");
        }

        // Find boundaries
        sb.AppendLine("");
        sb.AppendLine("PHÂN TÍCH BIÊN:");
        AnalyzeBoundaries(sb);

        return sb.ToString();
    }

    private void AnalyzeBoundaries(StringBuilder sb)
    {
        // Group by each parameter, find transition points
        var paramNames = Results.SelectMany(r => r.ConfigValues.Keys).Distinct().ToList();

        foreach (var param in paramNames)
        {
            var paramResults = Results
                .Where(r => r.ConfigValues.ContainsKey(param))
                .OrderBy(r => r.ConfigValues[param])
                .ToList();

            if (paramResults.Count < 2) continue;

            // Find first TLE
            var firstTle = paramResults.FirstOrDefault(r => r.TleCount > 0);
            if (firstTle != null)
            {
                sb.AppendLine($"  {param}: TLE đầu tiên ở {param} = {firstTle.ConfigValues[param]} (Pass rate: {firstTle.PassRate:F1}%)");
            }

            // Find first MLE
            var firstMle = paramResults.FirstOrDefault(r => r.MleCount > 0);
            if (firstMle != null)
            {
                sb.AppendLine($"  {param}: MLE đầu tiên ở {param} = {firstMle.ConfigValues[param]}");
            }

            // Find first WA
            var firstWa = paramResults.FirstOrDefault(r => r.WaCount > 0);
            if (firstWa != null)
            {
                sb.AppendLine($"  {param}: WA đầu tiên ở {param} = {firstWa.ConfigValues[param]}");
            }

            // Find where pass rate drops below 100%
            var firstFail = paramResults.FirstOrDefault(r => r.PassRate < 100);
            if (firstFail != null && firstFail != firstTle && firstFail != firstMle && firstFail != firstWa)
            {
                sb.AppendLine($"  {param}: Bắt đầu fail ở {param} = {firstFail.ConfigValues[param]} (Pass: {firstFail.PassRate:F1}%)");
            }
        }
    }

    public string ToJson()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(this, options);
    }
}

/// <summary>Stress test các cấu hình tham số để tìm biên độ thất bại.</summary>
public static class ConfigStressTester
{
    /// <summary>Chạy stress test theo spec.</summary>
    public static StressTestReport Run(StressTestSpec spec, IJobLog log, CancellationToken ct = default)
    {
        var report = new StressTestReport();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. Validate
            if (spec.ParameterRanges.Count == 0)
            {
                report.Error = "Cần ít nhất một ParameterRange";
                return report;
            }
            if (!File.Exists(spec.SolutionPath))
            {
                report.Error = $"Không tìm thấy lời giải: {spec.SolutionPath}";
                return report;
            }
            if (spec.ReferencePath != null && !File.Exists(spec.ReferencePath))
            {
                report.Error = $"Không tìm thấy lời giải tham chiếu: {spec.ReferencePath}";
                return report;
            }

            // 2. Generate all config combinations
            var configs = GenerateConfigCombinations(spec.BaseConfig, spec.ParameterRanges, spec.Problem);
            log.Line($"Tổng số cấu hình cần test: {configs.Count}");

            // 3. Prepare solution
            log.Line("Chuẩn bị lời giải…");
            var tempDir = Path.Combine(Path.GetTempPath(), "OjGen-Stress-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var solutionDir = Path.Combine(tempDir, "solution");
            Directory.CreateDirectory(solutionDir);

            var solution = SolutionRunner.Prepare(spec.SolutionPath, new Settings(), solutionDir, false, log, ct);
            if (!solution.Ok)
            {
                report.Error = "Lời giải: " + solution.Error;
                return report;
            }

            List<string>? referenceArgv = null;
            string? referenceCwd = null;
            if (spec.ReferencePath != null)
            {
                log.Line("Chuẩn bị lời giải tham chiếu…");
                var refDir = Path.Combine(tempDir, "reference");
                Directory.CreateDirectory(refDir);
                var reference = SolutionRunner.Prepare(spec.ReferencePath, new Settings(), refDir, false, log, ct);
                if (!reference.Ok)
                {
                    report.Error = "Lời giải tham chiếu: " + reference.Error;
                    return report;
                }
                referenceArgv = reference.Argv;
                referenceCwd = string.IsNullOrEmpty(reference.WorkingDirectory) ? refDir : reference.WorkingDirectory;
            }

            // 4. Test each config
            string inputPath = Path.Combine(tempDir, "stress.inp");
            string outPath = Path.Combine(tempDir, "stress.out");
            string errPath = Path.Combine(tempDir, "stress.err");
            string refOutPath = Path.Combine(tempDir, "stress.ref.out");

            int configIndex = 0;
            foreach (var (config, configName) in configs)
            {
                if (ct.IsCancellationRequested)
                {
                    report.Ok = false;
                    report.Error = "Đã hủy";
                    break;
                }

                configIndex++;
                log.Line($"[{configIndex}/{configs.Count}] {configName}");

                // Validate config
                var validationError = Generator.ValidateFullConfig(spec.Problem, config);
                if (validationError != null)
                {
                    log.Line($"  Bỏ qua (cấu hình không hợp lệ: {validationError})");
                    continue;
                }

                var result = TestSingleConfig(
                    spec.Problem, config, configName,
                    spec.TestsPerConfig, spec.BaseSeed,
                    solution, solutionDir,
                    referenceArgv, referenceCwd,
                    spec.Limits, spec.IgnoreWhitespace,
                    log, ct, inputPath, outPath, errPath, refOutPath);

                report.Results.Add(result);
                report.TotalConfigsTested++;
                report.TotalTestsRun += result.TestCount;
            }

            report.Ok = true;
            stopwatch.Stop();
            report.TotalTime = stopwatch.Elapsed;
            log.Line($"Hoàn tất: {report.TotalConfigsTested} cấu hình, {report.TotalTestsRun} test trong {report.TotalTime.TotalSeconds:F1}s");
            return report;
        }
        catch (Exception ex)
        {
            report.Error = "Lỗi stress test: " + ex.Message;
            stopwatch.Stop();
            report.TotalTime = stopwatch.Elapsed;
            return report;
        }
        finally
        {
            // Cleanup
            try
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "OjGen-Stress-*");
                foreach (string dir in Directory.GetDirectories(Path.GetTempPath(), "OjGen-Stress-*"))
                {
                    if (Directory.GetCreationTimeUtc(dir) < DateTime.UtcNow.AddMinutes(-10))
                        Directory.Delete(dir, true);
                }
            }
            catch { }
        }
    }

    private static List<(Config Config, string Name)> GenerateConfigCombinations(Config baseConfig, List<ParameterRange> ranges, Problem problem)
    {
        var results = new List<(Config, string)>();

        // Build value lists for each parameter
        var paramValues = new Dictionary<string, List<long>>();
        foreach (var range in ranges)
        {
            var values = new List<long>();
            if (range.Values != null && range.Values.Count > 0)
            {
                values.AddRange(range.Values);
            }
            else
            {
                for (long v = range.Min; v <= range.Max; v += range.Step)
                    values.Add(v);
            }
            paramValues[range.Key] = values;
        }

        // Generate all combinations (Cartesian product)
        var keys = paramValues.Keys.ToArray();
        var indices = new int[keys.Length];
        bool done = false;

        while (!done)
        {
            var config = baseConfig.Clone();
            var nameParts = new List<string>();

            for (int i = 0; i < keys.Length; i++)
            {
                long value = paramValues[keys[i]][indices[i]];
                config.Set(keys[i], value);
                nameParts.Add($"{keys[i]}={value}");
            }

            // Apply problem-specific adjustments
            foreach (var param in problem.Params)
            {
                if (paramValues.ContainsKey(param.Key))
                    problem.AdjustConfig(config, param.Key);
            }

            results.Add((config, string.Join(", ", nameParts)));

            // Increment indices
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                indices[i]++;
                if (indices[i] < paramValues[keys[i]].Count)
                    break;
                if (i == 0)
                {
                    done = true;
                    break;
                }
                indices[i] = 0;
            }
        }

        return results;
    }

    private static StressTestResult TestSingleConfig(
        Problem problem, Config config, string configName,
        int testCount, ulong? baseSeed,
        PreparedSolution solution, string solutionDir,
        List<string>? referenceArgv, string? referenceCwd,
        RunLimits limits, bool ignoreWhitespace,
        IJobLog log, CancellationToken ct,
        string inputPath, string outPath, string errPath, string refOutPath)
    {
        var result = new StressTestResult
        {
            ConfigName = configName,
            ConfigValues = new Dictionary<string, long>(config.Ints),
            TestCount = testCount
        };

        var times = new List<long>();
        var memories = new List<ulong>();

        // Use a deterministic seed per config if baseSeed provided
        ulong configSeed = baseSeed ?? (ulong)Environment.TickCount;
        var rng = new Rng(configSeed);

        for (int i = 0; i < testCount; i++)
        {
            if (ct.IsCancellationRequested) break;

            // Generate input
            string input = problem.Generate(rng, config);
            if (problem.ValidateInput(input, config) != null)
            {
                log.Line($"  Test {i+1}: Input không hợp lệ");
                continue;
            }

            if (!FileManager.WriteFile(inputPath, input, out _))
                continue;

            // Run solution
            var run = ProcessRunner.Run(solution.Argv, inputPath, outPath, errPath,
                string.IsNullOrEmpty(solution.WorkingDirectory) ? solutionDir : solution.WorkingDirectory,
                limits, ct, unixLimits: true);

            times.Add(run.ElapsedMs);
            memories.Add(run.PeakMemoryBytes);

            bool isWa = false;

            switch (run.Status)
            {
                case RunStatus.Ok:
                    if (referenceArgv != null)
                    {
                        // Check WA with reference
                        var refRun = ProcessRunner.Run(referenceArgv, inputPath, refOutPath, errPath,
                            referenceCwd!, limits, ct, unixLimits: true);
                        if (refRun.Status == RunStatus.Ok)
                        {
                            string candidateOut = FileManager.ReadText(outPath);
                            string referenceOut = FileManager.ReadText(refOutPath);
                            if (!OutputsEqual(candidateOut, referenceOut, ignoreWhitespace))
                            {
                                isWa = true;
                                result.WaCount++;
                            }
                            else
                            {
                                result.OkCount++;
                            }
                        }
                        else
                        {
                            // Reference failed - count as error
                            result.ReCount++;
                        }
                    }
                    else
                    {
                        result.OkCount++;
                    }
                    break;

                case RunStatus.Timeout:
                    result.TleCount++;
                    break;
                case RunStatus.RuntimeError:
                    // Check if it's a memory limit error
                    if (limits.MemoryBytes > 0 && run.Message.Contains("Vượt giới hạn bộ nhớ", StringComparison.OrdinalIgnoreCase))
                        result.MleCount++;
                    else
                        result.ReCount++;
                    break;
                default:
                    result.ReCount++;
                    break;
            }

            if (isWa)
                log.Line($"  Test {i+1}: WA");
            else if (run.Status != RunStatus.Ok)
                log.Line($"  Test {i+1}: {ProcessRunner.StatusName(run.Status)}");
        }

        // Calculate statistics
        if (times.Count > 0)
        {
            times.Sort();
            memories.Sort();
            result.MaxTimeMs = times[^1];
            result.MedianTimeMs = times[times.Count / 2];
            int p95Idx = Math.Min(times.Count - 1, (int)Math.Ceiling(times.Count * 0.95));
            result.P95TimeMs = times[p95Idx];
            result.PeakMemoryBytes = (long)memories[^1];
        }

        log.Line($"  Kết quả: OK={result.OkCount}, TLE={result.TleCount}, MLE={result.MleCount}, RE={result.ReCount}, WA={result.WaCount}, Pass={result.PassRate:F1}%, Max={result.MaxTimeMs}ms, Med={result.MedianTimeMs}ms, P95={result.P95TimeMs}ms, Mem={result.PeakMemoryBytes / (1024*1024)}MB");

        return result;
    }

    private static bool OutputsEqual(string a, string b, bool ignoreWhitespace)
    {
        if (ignoreWhitespace)
        {
            a = NormalizeWhitespace(a);
            b = NormalizeWhitespace(b);
        }
        else
        {
            a = a.Replace("\r\n", "\n").Replace('\r', '\n');
            b = b.Replace("\r\n", "\n").Replace('\r', '\n');
        }
        return string.Equals(a, b, StringComparison.Ordinal);
    }

    private static string NormalizeWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (char c in text.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace) sb.Append(' ');
            sb.Append(c);
            pendingSpace = false;
        }
        return sb.ToString();
    }
}