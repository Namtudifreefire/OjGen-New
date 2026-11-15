using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OjGen.Core;

/// <summary>Kết quả quá trình thu nhỏ test case.</summary>
public sealed class ShrinkResult
{
    public bool Ok { get; set; }
    public bool Cancelled { get; set; }
    public string? Error { get; set; }
    public string OriginalInput { get; set; } = "";
    public string MinimizedInput { get; set; } = "";
    public int OriginalSize { get; set; }
    public int MinimizedSize { get; set; }
    public int Trials { get; set; }
    public int SuccessfulReductions { get; set; }
    public RunStatus FinalStatus { get; set; }
    public string? FinalMessage { get; set; }
    public TimeSpan TotalTime { get; set; }
}

/// <summary>Cấu hình cho quá trình thu nhỏ.</summary>
public sealed class ShrinkSpec
{
    public Problem Problem { get; set; } = null!;
    public Config Cfg { get; set; } = new();
    public string InputPath { get; set; } = "";
    public string SolutionPath { get; set; } = "";
    public string? ReferencePath { get; set; } // Optional: for differential shrink (WA vs reference)
    public RunLimits Limits { get; set; } = new();
    public string? WorkingDirectory { get; set; }
    public bool IgnoreWhitespace { get; set; } = true;
    public int MaxTrials { get; set; } = 500;
    public int Seed { get; set; } = 0; // For reproducible candidate ordering
    public bool Aggressive { get; set; } = false; // Try more aggressive reductions
}

/// <summary>Thu nhỏ test case giữ nguyên trạng thái thất bại (WA/TLE/MLE/RE).</summary>
public static class Shrinker
{
    private static readonly Regex IntegerToken = new(@"(?<![A-Za-z0-9_])(-?\d+)", RegexOptions.Compiled);
    private static readonly HashSet<string> SizeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "n", "minn", "maxn", "m", "minm", "maxm", "q", "minq", "maxq",
        "t", "mint", "maxt", "len", "minlen", "maxlen", "length", "minlength", "maxlength",
        "h", "w", "rows", "cols", "size", "count",
    };

    /// <summary>Chạy quá trình thu nhỏ.</summary>
    public static ShrinkResult Run(ShrinkSpec spec, IJobLog log, CancellationToken ct = default)
    {
        var result = new ShrinkResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. Validate inputs
            if (!File.Exists(spec.InputPath))
            {
                result.Error = $"Không tìm thấy file input: {spec.InputPath}";
                return result;
            }
            if (!File.Exists(spec.SolutionPath))
            {
                result.Error = $"Không tìm thấy lời giải: {spec.SolutionPath}";
                return result;
            }
            if (spec.ReferencePath != null && !File.Exists(spec.ReferencePath))
            {
                result.Error = $"Không tìm thấy lời giải tham chiếu: {spec.ReferencePath}";
                return result;
            }

            string originalInput = FileManager.ReadText(spec.InputPath);
            result.OriginalInput = originalInput;
            result.OriginalSize = originalInput.Length;

            // 2. Prepare solution(s)
            log.Line("Chuẩn bị lời giải…");
            var tempDir = Path.Combine(Path.GetTempPath(), "OjGen-Shrink-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var solutionDir = Path.Combine(tempDir, "solution");
            Directory.CreateDirectory(solutionDir);
            var solution = SolutionRunner.Prepare(spec.SolutionPath, new Settings(), solutionDir, false, log, ct);
            if (!solution.Ok)
            {
                result.Error = "Lời giải: " + solution.Error;
                return result;
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
                    result.Error = "Lời giải tham chiếu: " + reference.Error;
                    return result;
                }
                referenceArgv = reference.Argv;
                referenceCwd = string.IsNullOrEmpty(reference.WorkingDirectory) ? refDir : reference.WorkingDirectory;
            }

            // 3. Run original input to confirm failure and capture status
            log.Line("Chạy test gốc để xác nhận thất bại…");
            string inputPath = Path.Combine(tempDir, "shrink.inp");
            string outPath = Path.Combine(tempDir, "shrink.out");
            string errPath = Path.Combine(tempDir, "shrink.err");

            if (!FileManager.WriteFile(inputPath, originalInput, out _))
            {
                result.Error = "Không ghi được file input tạm.";
                return result;
            }

            var originalRun = ProcessRunner.Run(solution.Argv, inputPath, outPath, errPath,
                string.IsNullOrEmpty(solution.WorkingDirectory) ? solutionDir : solution.WorkingDirectory,
                spec.Limits, ct, unixLimits: true);

            // Check if we have a reference solution and the candidate produces different output (WA case)
            bool isWrongAnswer = false;
            if (originalRun.Status == RunStatus.Ok && referenceArgv != null)
            {
                RunResult refRun = ProcessRunner.Run(referenceArgv, inputPath, outPath + ".ref", errPath + ".ref",
                    referenceCwd!, spec.Limits, ct, unixLimits: true);
                if (refRun.Status == RunStatus.Ok && !ct.IsCancellationRequested)
                {
                    string candidateOut = FileManager.ReadText(outPath);
                    string referenceOut = FileManager.ReadText(outPath + ".ref");
                    if (!OutputsEqual(candidateOut, referenceOut, spec.IgnoreWhitespace))
                    {
                        isWrongAnswer = true;
                        originalRun = refRun; // Use reference run for status tracking
                        log.Line($"Test gốc: WA (output khác lời giải tham chiếu)");
                    }
                }
            }

            if (originalRun.Status == RunStatus.Ok && !isWrongAnswer)
            {
                result.Error = "Test gốc chạy THÀNH CÔNG (OK). Không có gì để thu nhỏ.";
                return result;
            }

            result.FinalStatus = originalRun.Status;
            result.FinalMessage = isWrongAnswer ? "Wrong Answer (output khác lời giải tham chiếu)" : originalRun.Message;
            log.Line($"Test gốc: {ProcessRunner.StatusName(originalRun.Status)} — {result.FinalMessage}");

            // Store the failure type for the predicate
            RunStatus targetStatus = originalRun.Status;
            bool isWaCase = isWrongAnswer;

            // 4. Define the failure predicate
            bool StillFails(string candidateInput)
            {
                if (spec.Problem.ValidateInput(candidateInput, spec.Cfg) != null)
                    return false; // Invalid input

                if (!FileManager.WriteFile(inputPath, candidateInput, out _))
                    return false;

                RunResult run = ProcessRunner.Run(solution.Argv, inputPath, outPath, errPath,
                    string.IsNullOrEmpty(solution.WorkingDirectory) ? solutionDir : solution.WorkingDirectory,
                    spec.Limits, ct, unixLimits: true);

                if (ct.IsCancellationRequested)
                    return false;

                // For WA case: candidate must still run Ok but produce different output from reference
                if (isWaCase && referenceArgv != null)
                {
                    if (run.Status != RunStatus.Ok)
                        return false; // Status changed (e.g., crash -> not WA anymore)

                    RunResult refRun = ProcessRunner.Run(referenceArgv, inputPath, outPath + ".ref", errPath + ".ref",
                        referenceCwd!, spec.Limits, ct, unixLimits: true);
                    if (refRun.Status != RunStatus.Ok || ct.IsCancellationRequested)
                        return false;
                    return !OutputsEqual(FileManager.ReadText(outPath), FileManager.ReadText(outPath + ".ref"), spec.IgnoreWhitespace);
                }

                // For other failures (TLE, MLE, RE): status must match
                if (run.Status != targetStatus)
                    return false;

                return true; // Same failure status preserved
            }

            // 5. Run minimization
            log.Line($"Bắt đầu thu nhỏ (giới hạn {spec.MaxTrials} thử)…");
            var (minimized, trials, reductions) = Minimize(originalInput, spec.Problem, spec.Cfg, StillFails, log, ct, spec.MaxTrials, spec.Aggressive);

            result.MinimizedInput = minimized;
            result.MinimizedSize = minimized.Length;
            result.Trials = trials;
            result.SuccessfulReductions = reductions;
            result.Ok = true;

            // 6. Final verification
            if (minimized.Length < originalInput.Length)
            {
                log.Line($"Thành công! Kích thước: {originalInput.Length} → {minimized.Length} ký tự (giảm {100.0 * (originalInput.Length - minimized.Length) / originalInput.Length:F1}%)");
                log.Line($"Thử: {trials}, Giảm thành công: {reductions}");
            }
            else
            {
                log.Line("Không thể thu nhỏ thêm (đã tối thiểu).");
            }

            // Save minimized input next to original
            string minimizedPath = Path.ChangeExtension(spec.InputPath, ".min.inp");
            FileManager.WriteFile(minimizedPath, minimized, out _);
            log.Line($"Đã lưu: {minimizedPath}");

            stopwatch.Stop();
            result.TotalTime = stopwatch.Elapsed;
            return result;
        }
        catch (Exception ex)
        {
            result.Error = "Lỗi khi thu nhỏ: " + ex.Message;
            return result;
        }
        finally
        {
            // Cleanup temp dir
            try
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "OjGen-Shrink-*");
                foreach (string dir in Directory.GetDirectories(Path.GetTempPath(), "OjGen-Shrink-*"))
                {
                    if (Directory.GetCreationTimeUtc(dir) < DateTime.UtcNow.AddMinutes(-10))
                        Directory.Delete(dir, true);
                }
            }
            catch { }
        }
    }

    private static (string Minimized, int Trials, int Reductions) Minimize(
        string input, Problem problem, Config config,
        Func<string, bool> stillFails, IJobLog log, CancellationToken ct,
        int maxTrials, bool aggressive)
    {
        string best = input;
        int trials = 0;
        int reductions = 0;

        var candidates = GenerateShrinkCandidates(input, problem, config, aggressive).ToList();
        log.Line($"Tổng số ứng viên thu nhỏ: {candidates.Count}");

        bool improved;
        do
        {
            improved = false;
            foreach (string candidate in candidates)
            {
                if (ct.IsCancellationRequested || trials >= maxTrials)
                    return (best, trials, reductions);

                if (candidate.Length >= best.Length)
                    continue;

                trials++;
                if (stillFails(candidate))
                {
                    best = candidate;
                    reductions++;
                    improved = true;
                    log.Line($"  Thu nhỏ: còn {best.Length} ký tự (thử {trials}/{maxTrials})");
                    // Regenerate candidates for the new smaller input
                    candidates = GenerateShrinkCandidates(best, problem, config, aggressive).ToList();
                    break;
                }
            }
        } while (improved && trials < maxTrials);

        return (best, trials, reductions);
    }

    /// <summary>Sinh các ứng viên thu nhỏ cho một input.</summary>
    public static IEnumerable<string> GenerateShrinkCandidates(string input, Problem problem, Config config, bool aggressive = false)
    {
        string normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');

        // Problem-specific structural shrinking
        foreach (var candidate in GenerateStructuralCandidates(normalized, lines, problem, config))
            yield return candidate;

        // Generic integer replacement (boundary values)
        foreach (var candidate in GenerateIntegerReplacements(normalized))
            yield return candidate;

        // Aggressive: try removing individual lines/tokens
        if (aggressive)
        {
            foreach (var candidate in GenerateAggressiveCandidates(normalized, lines, problem, config))
                yield return candidate;
        }
    }

    private static IEnumerable<string> GenerateStructuralCandidates(string input, string[] lines, Problem problem, Config config)
    {
        // ARRAY: reduce array size
        if (problem.Id == "array" && lines.Length >= 2 && Util.ParseLL(lines[0].Trim(), out long arrayCount))
        {
            var values = lines[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int chunk = Math.Max(1, values.Count / 2); chunk >= 1; chunk /= 2)
            {
                for (int start = 0; start + chunk <= values.Count; start++)
                {
                    var reduced = values.Where((_, i) => i < start || i >= start + chunk).ToList();
                    if (reduced.Count > 0 && reduced.Count < arrayCount)
                        yield return reduced.Count + "\n" + string.Join(' ', reduced) + "\n";
                }
                if (chunk == 1) break;
            }
        }
        // STRING: reduce string length
        else if (problem.Id == "string")
        {
            bool hasLength = config.B("printLen");
            int stringLine = hasLength ? 1 : 0;
            if (lines.Length > stringLine)
            {
                string value = lines[stringLine];
                for (int chunk = Math.Max(1, value.Length / 2); chunk >= 1; chunk /= 2)
                {
                    for (int start = 0; start + chunk <= value.Length; start++)
                    {
                        string reduced = value.Remove(start, chunk);
                        yield return (hasLength ? reduced.Length + "\n" : "") + reduced + "\n";
                    }
                    if (chunk == 1) break;
                }
            }
        }
        // GRAPH: reduce edges
        else if (problem.Id == "graph" && lines.Length >= 2)
        {
            var header = lines[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length == 2 && Util.ParseLL(header[1], out long m))
            {
                var edges = lines.Skip(1).Where(line => line.Trim().Length > 0).ToList();
                for (int chunk = Math.Max(1, edges.Count / 2); chunk >= 1; chunk /= 2)
                {
                    for (int start = 0; start + chunk <= edges.Count; start++)
                    {
                        var reduced = edges.Where((_, i) => i < start || i >= start + chunk).ToList();
                        if (reduced.Count < m && reduced.Count > 0)
                            yield return header[0] + " " + reduced.Count + "\n" + string.Join("\n", reduced) + "\n";
                    }
                    if (chunk == 1) break;
                }
            }
        }
        // TREE: reduce nodes (keep prefix connected)
        else if (problem.Id == "tree" && lines.Length >= 1 && Util.ParseLL(lines[0].Trim(), out long treeN))
        {
            int treeCount = (int)Math.Clamp(treeN, 1, int.MaxValue);
            for (int chunk = Math.Max(1, treeCount / 2); chunk >= 1; chunk /= 2)
            {
                for (int start = Math.Max(1, treeCount - chunk + 1); start >= 2; start--)
                {
                    int reducedN = start - 1;
                    if (reducedN < treeCount)
                        yield return ShrinkTree(lines, reducedN, config);
                }
                if (chunk == 1) break;
            }
        }
        // MATRIX: reduce rows/cols
        else if (problem.Id == "matrix" && lines.Length >= 2 && Util.ParseLL(lines[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0], out long matrixN))
        {
            string[] header = lines[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length == 2 && Util.ParseLL(header[1], out long matrixM))
            {
                for (int remove = Math.Max(1, (int)matrixN / 2); remove >= 1; remove /= 2)
                {
                    if (matrixN - remove > 0 && matrixN - remove < matrixN)
                    {
                        var rows = lines.Skip(1).Take((int)(matrixN - remove)).ToArray();
                        yield return $"{matrixN - remove} {matrixM}\n{string.Join('\n', rows)}\n";
                    }
                    if (matrixM - remove > 0 && matrixM - remove < matrixM)
                    {
                        var rows = lines.Skip(1).Take((int)matrixN).Select(row =>
                        {
                            var values = row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                            return string.Join(' ', values.Take((int)(matrixM - remove)));
                        });
                        yield return $"{matrixN} {matrixM - remove}\n{string.Join('\n', rows)}\n";
                    }
                    if (remove == 1) break;
                }
            }
            else if (header.Length == 1 && Util.ParseLL(header[0], out long fixedRows))
            {
                if (config.I("layout") == 2) // Two arrays
                {
                    var first = lines.Length > 1 ? lines[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
                    var second = lines.Length > 2 ? lines[2].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
                    for (int remove = Math.Max(1, (int)fixedRows / 2); remove >= 1; remove /= 2)
                    {
                        int reducedRows = (int)fixedRows - remove;
                        if (reducedRows > 0)
                            yield return $"{reducedRows}\n{string.Join(' ', first.Take(reducedRows))}\n{string.Join(' ', second.Take(reducedRows))}\n";
                        if (remove == 1) break;
                    }
                }
                else // Single column matrix (layout 1 with only n)
                {
                    for (int remove = Math.Max(1, (int)fixedRows / 2); remove >= 1; remove /= 2)
                    {
                        int reducedRows = (int)fixedRows - remove;
                        if (reducedRows > 0)
                            yield return $"{reducedRows}\n{string.Join('\n', lines.Skip(1).Take(reducedRows))}\n";
                        if (remove == 1) break;
                    }
                }
            }
        }
        // GRID: reduce rows/cols
        else if (problem.Id == "grid" && lines.Length >= 2)
        {
            string[] header = lines[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length == 2 && Util.ParseLL(header[0], out long rows) && Util.ParseLL(header[1], out long cols))
            {
                for (int remove = Math.Max(1, (int)rows / 2); remove >= 1; remove /= 2)
                {
                    int reducedRows = (int)rows - remove;
                    if (reducedRows > 0)
                        yield return RebuildGrid(lines, reducedRows, (int)cols, config.B("markers"));
                    if (remove == 1) break;
                }
                for (int remove = Math.Max(1, (int)cols / 2); remove >= 1; remove /= 2)
                {
                    int m = (int)cols - remove;
                    if (m > 0)
                        yield return RebuildGrid(lines, (int)rows, m, config.B("markers"));
                    if (remove == 1) break;
                }
            }
        }
        // QUERIES: reduce queries
        else if (problem.Id == "queries" && lines.Length >= 3)
        {
            string[] header = lines[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length == 2 && Util.ParseLL(header[0], out long arrayLength) && Util.ParseLL(header[1], out long q))
            {
                var queryLines = lines.Skip(2).Where(line => line.Trim().Length > 0).ToList();
                for (int chunk = Math.Max(1, queryLines.Count / 2); chunk >= 1; chunk /= 2)
                {
                    for (int start = 0; start + chunk <= queryLines.Count; start++)
                    {
                        var reduced = queryLines.Where((_, i) => i < start || i >= start + chunk).ToList();
                        if (reduced.Count > 0 && reduced.Count < q)
                            yield return $"{arrayLength} {reduced.Count}\n{lines[1]}\n{string.Join('\n', reduced)}\n";
                    }
                    if (chunk == 1) break;
                }
            }
        }
        // MULTI_TEST: reduce subtests
        else if (problem.Id == "multi_test" && lines.Length >= 3 && Util.ParseLL(lines[0].Trim(), out long testCount))
        {
            var testBlocks = new List<string[]>();
            for (int line = 1; line + 1 < lines.Length; line += 2)
                testBlocks.Add(new[] { lines[line], lines[line + 1] });
            for (int chunk = Math.Max(1, testBlocks.Count / 2); chunk >= 1; chunk /= 2)
            {
                for (int start = 0; start + chunk <= testBlocks.Count; start++)
                {
                    var reduced = testBlocks.Where((_, i) => i < start || i >= start + chunk).ToList();
                    if (reduced.Count > 0 && reduced.Count < testCount)
                        yield return $"{reduced.Count}\n{string.Join('\n', reduced.SelectMany(block => block))}\n";
                }
                if (chunk == 1) break;
            }
        }
        // PREFIX_SUM_RANGE / PREFIX_SUBSEQUENCE_RANGE: similar to queries
        else if ((problem.Id == "prefix_sum_range" || problem.Id == "prefix_subsequence_range") && lines.Length >= 3)
        {
            string[] header = lines[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length == 2 && Util.ParseLL(header[0], out long n) && Util.ParseLL(header[1], out long q))
            {
                int payloadLines = problem.Id == "prefix_sum_range" ? 1 : 1; // array line
                var queryLines = lines.Skip(1 + payloadLines).Where(line => line.Trim().Length > 0).ToList();
                for (int chunk = Math.Max(1, queryLines.Count / 2); chunk >= 1; chunk /= 2)
                {
                    for (int start = 0; start + chunk <= queryLines.Count; start++)
                    {
                        var reduced = queryLines.Where((_, i) => i < start || i >= start + chunk).ToList();
                        if (reduced.Count > 0 && reduced.Count < q)
                        {
                            string prefix = string.Join('\n', lines.Take(1 + payloadLines));
                            yield return $"{prefix}\n{string.Join('\n', reduced)}\n";
                        }
                    }
                    if (chunk == 1) break;
                }
            }
        }
        // CHESS: reduce board positions (for knight moves/targets/reachability)
        else if (problem.Id.StartsWith("chess_") && lines.Length >= 1)
        {
            // Chess problems typically have 1-2 lines with coordinates
            // Try reducing coordinate values to boundary values
            foreach (Match match in IntegerToken.Matches(input))
            {
                if (!long.TryParse(match.Value, out long value)) continue;
                // Chess coordinates are typically 0-7, try boundary values
                var replacements = new HashSet<long> { 0, 7, value / 2 };
                if (value > 0) replacements.Add(value - 1);
                if (value < 7) replacements.Add(value + 1);
                foreach (long replacement in replacements)
                {
                    if (replacement == value) continue;
                    yield return input[..match.Index] + replacement.ToString(CultureInfo.InvariantCulture) + input[(match.Index + match.Length)..];
                }
            }
        }
        // CUSTOM / GENERIC: try reducing first number (often n)
        else if (lines.Length >= 1 && Util.ParseLL(lines[0].Trim(), out long firstNum))
        {
            for (long reduced = Math.Max(1, firstNum / 2); reduced < firstNum; reduced = Math.Max(reduced + 1, firstNum - (firstNum - reduced) / 2))
            {
                string newFirst = lines[0].Trim().Replace(firstNum.ToString(), reduced.ToString());
                if (newFirst != lines[0].Trim())
                    yield return newFirst + (lines.Length > 1 ? "\n" + string.Join("\n", lines.Skip(1)) : "") + "\n";
                if (reduced == firstNum - 1) break;
            }
        }
    }

    private static IEnumerable<string> GenerateIntegerReplacements(string input)
    {
        foreach (Match match in IntegerToken.Matches(input))
        {
            if (!long.TryParse(match.Value, out long value)) continue;
            var replacements = new HashSet<long> { 0, 1, -1, value / 2 };
            if (value > 0) replacements.Add(value - 1);
            if (value < 0) replacements.Add(value + 1);
            if (value > 1) replacements.Add(2);
            if (value < -1) replacements.Add(-2);
            foreach (long replacement in replacements)
            {
                if (replacement == value) continue;
                yield return input[..match.Index] + replacement.ToString(CultureInfo.InvariantCulture) + input[(match.Index + match.Length)..];
            }
        }
    }

    private static IEnumerable<string> GenerateAggressiveCandidates(string input, string[] lines, Problem problem, Config config)
    {
        // Try removing each line (except header)
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var reduced = lines.Where((_, idx) => idx != i).ToArray();
            yield return string.Join("\n", reduced) + "\n";
        }

        // Try removing tokens from each line
        for (int i = 0; i < lines.Length; i++)
        {
            var tokens = lines[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            for (int chunk = Math.Max(1, tokens.Length / 2); chunk >= 1; chunk /= 2)
            {
                for (int start = 0; start + chunk <= tokens.Length; start++)
                {
                    var reducedTokens = tokens.Where((_, idx) => idx < start || idx >= start + chunk).ToArray();
                    if (reducedTokens.Length > 0 && reducedTokens.Length < tokens.Length)
                    {
                        var newLines = lines.ToArray();
                        newLines[i] = string.Join(' ', reducedTokens);
                        yield return string.Join("\n", newLines) + "\n";
                    }
                }
                if (chunk == 1) break;
            }
        }
    }

    private static string ShrinkTree(string[] lines, int n, Config config)
    {
        if (config.I("layout") == 1)
        {
            string[] parents = lines.Length > 1 ? lines[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
            return n == 1 ? "1\n" : $"{n}\n{string.Join(' ', parents.Take(n - 1))}\n";
        }
        int width = config.B("weighted") ? 3 : 2;
        var edges = new List<string>();
        foreach (string line in lines.Skip(1))
        {
            string[] edge = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (edge.Length < 2 || !Util.ParseLL(edge[0], out long u) || !Util.ParseLL(edge[1], out long v)) continue;
            if (u > n || v > n) continue;
            edges.Add(string.Join(' ', edge.Take(width)));
        }
        return $"{n}\n{string.Join('\n', edges)}\n";
    }

    private static string RebuildGrid(string[] lines, int rows, int cols, bool markers)
    {
        var grid = new List<char[]>();
        for (int i = 0; i < rows && i + 1 < lines.Length; i++)
            grid.Add(lines[i + 1].PadRight(cols, '.').Take(cols).ToArray());
        while (grid.Count < rows) grid.Add(Enumerable.Repeat('.', cols).ToArray());
        if (markers && rows * cols >= 2)
        {
            char filler = grid.SelectMany(row => row).FirstOrDefault(ch => ch is not ('S' or 'T'));
            if (filler == '\0') filler = '.';
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (grid[r][c] is 'S' or 'T') grid[r][c] = filler;
            grid[0][0] = 'S';
            grid[rows - 1][cols - 1] = 'T';
        }
        return $"{rows} {cols}\n{string.Join('\n', grid.Select(row => new string(row)))}\n";
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