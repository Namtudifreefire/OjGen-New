using System.Text;

namespace OjGen.Core;

public sealed class DifferentialSpec
{
    public Problem Problem { get; set; } = null!;
    public Config Cfg { get; set; } = new();
    public int TestCount { get; set; } = 100;
    public ulong Seed { get; set; }
    public string ReferencePath { get; set; } = "";
    public string CandidatePath { get; set; } = "";
    public bool IgnoreWhitespace { get; set; } = true;
    public bool MinimizeCounterexamples { get; set; } = true;
    public int MaxMinimizationTrials { get; set; } = 64;
}

public sealed class DifferentialFailure
{
    public int TestIndex { get; set; }
    public string Input { get; set; } = "";
    public string ReferenceOutput { get; set; } = "";
    public string CandidateOutput { get; set; } = "";
    public string Reason { get; set; } = "";
    public string MinimizedInput { get; set; } = "";
    public int MinimizationTrials { get; set; }
}

public sealed class DifferentialResult
{
    public bool Ok { get; set; }
    public bool Cancelled { get; set; }
    public int Tested { get; set; }
    public int Mismatches { get; set; }
    public string Error { get; set; } = "";
    public List<DifferentialFailure> Failures { get; } = new();
}

/// <summary>Chạy lời giải tham chiếu và lời giải cần kiểm tra trên cùng các test nhỏ.</summary>
public static class DifferentialTester
{
    private static readonly System.Text.RegularExpressions.Regex IntegerToken =
        new(@"(?<![A-Za-z0-9_])-?\d+", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly HashSet<string> SizeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "n", "minN", "maxN", "m", "minM", "maxM", "q", "minQ", "maxQ",
        "t", "minT", "maxT", "len", "minLen", "maxLen", "length", "minLength", "maxLength",
        "h", "w", "rows", "cols", "size", "count",
    };

    /// <summary>Reduce common size parameters for quick cross-check runs.</summary>
    public static Config CreateSmallConfig(Problem problem, Config source)
    {
        var config = source.Clone();
        foreach (var param in problem.Params)
        {
            if (param.Type != ParamType.Int || param.Options is { Count: > 0 } || !SizeKeys.Contains(param.Key)) continue;
            long target = param.Key.StartsWith("min", StringComparison.OrdinalIgnoreCase)
                ? param.Lo
                : Math.Clamp(20, param.Lo, param.Hi);
            config.Set(param.Key, target);
            problem.AdjustConfig(config, param.Key);
        }
        return Generator.ValidateFullConfig(problem, config) == null ? config : source.Clone();
    }

    private static string Normalize(string text, bool ignoreWhitespace)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (!ignoreWhitespace) return text;
        var sb = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (char c in text)
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

    private static IEnumerable<string> ShrinkCandidates(string input, Problem problem, Config config)
    {
        string[] lines = input.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        if (problem.Id == "array" && lines.Length >= 2 && Util.ParseLL(lines[0].Trim(), out long arrayCount))
        {
            var values = lines[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int chunk = Math.Max(1, values.Count / 2); chunk >= 1; chunk /= 2)
            {
                for (int start = 0; start + chunk <= values.Count; start++)
                {
                    var reduced = values.Where((_, i) => i < start || i >= start + chunk).ToList();
                    if (reduced.Count < arrayCount && reduced.Count > 0)
                        yield return reduced.Count + "\n" + string.Join(' ', reduced) + "\n";
                }
                if (chunk == 1) break;
            }
        }
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
                        if (reduced.Count < m)
                            yield return header[0] + " " + reduced.Count + "\n" + string.Join("\n", reduced) + "\n";
                    }
                    if (chunk == 1) break;
                }
            }
        }
        else if (problem.Id == "tree" && lines.Length >= 1 && Util.ParseLL(lines[0].Trim(), out long treeN))
        {
            // Tree generation numbers parents before children. Removing a suffix of
            // vertices therefore keeps the prefix connected and only needs re-counting.
            int treeCount = (int)Math.Clamp(treeN, 1, int.MaxValue);
            int reducedN = treeCount;
            for (int chunk = Math.Max(1, treeCount / 2); chunk >= 1; chunk /= 2)
            {
                for (int start = Math.Max(1, treeCount - chunk + 1); start >= 2; start--)
                {
                    reducedN = start - 1;
                    if (reducedN < treeCount)
                        yield return ShrinkTree(lines, reducedN, config);
                }
                if (chunk == 1) break;
            }
        }
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
                if (config.I("layout") == 2)
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
                else
                {
                    for (int remove = Math.Max(1, (int)fixedRows / 2); remove >= 1; remove /= 2)
                    {
                        int reducedRows = (int)fixedRows - remove;
                        if (reducedRows > 0) yield return $"{reducedRows}\n{string.Join('\n', lines.Skip(1).Take(reducedRows))}\n";
                        if (remove == 1) break;
                    }
                }
            }
        }
        else if (problem.Id == "grid" && lines.Length >= 2)
        {
            string[] header = lines[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length == 2 && Util.ParseLL(header[0], out long rows) && Util.ParseLL(header[1], out long cols))
            {
                for (int remove = Math.Max(1, (int)rows / 2); remove >= 1; remove /= 2)
                {
                    int reducedRows = (int)rows - remove;
                    if (reducedRows > 0) yield return RebuildGrid(lines, reducedRows, (int)cols, config.B("markers"));
                    if (remove == 1) break;
                }
                for (int remove = Math.Max(1, (int)cols / 2); remove >= 1; remove /= 2)
                {
                    int m = (int)cols - remove;
                    if (m > 0) yield return RebuildGrid(lines, (int)rows, m, config.B("markers"));
                    if (remove == 1) break;
                }
            }
        }
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

        foreach (System.Text.RegularExpressions.Match match in IntegerToken.Matches(input))
        {
            if (!long.TryParse(match.Value, out long value)) continue;
            var replacements = new HashSet<long> { 0, 1, -1, value / 2 };
            if (value > 0) replacements.Add(value - 1);
            if (value < 0) replacements.Add(value + 1);
            foreach (long replacement in replacements)
            {
                if (replacement == value) continue;
                yield return input[..match.Index] + replacement.ToString(System.Globalization.CultureInfo.InvariantCulture) + input[(match.Index + match.Length)..];
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

    private static (string Input, int Trials) MinimizeCounterexample(string input, Problem problem, Config config,
        List<string> referenceArgv, List<string> candidateArgv, RunLimits limits, string tempDir,
        bool ignoreWhitespace, int maxTrials, IJobLog log, CancellationToken ct)
    {
        string best = input;
        int trials = 0;
        string inputPath = Path.Combine(tempDir, "minimize.inp");
        string refOut = Path.Combine(tempDir, "minimize.ref.out");
        string refErr = Path.Combine(tempDir, "minimize.ref.err");
        string candidateOut = Path.Combine(tempDir, "minimize.candidate.out");
        string candidateErr = Path.Combine(tempDir, "minimize.candidate.err");

        bool StillFails(string candidateInput)
        {
            if (problem.ValidateInput(candidateInput, config) != null) return false;
            if (!FileManager.WriteFile(inputPath, candidateInput, out _)) return false;
            RunResult reference = ProcessRunner.Run(referenceArgv, inputPath, refOut, refErr, tempDir, limits, ct);
            if (reference.Status != RunStatus.Ok || ct.IsCancellationRequested) return false;
            RunResult candidate = ProcessRunner.Run(candidateArgv, inputPath, candidateOut, candidateErr, tempDir, limits, ct);
            if (candidate.Status != RunStatus.Ok) return false;
            return !string.Equals(Normalize(FileManager.ReadText(refOut), ignoreWhitespace),
                Normalize(FileManager.ReadText(candidateOut), ignoreWhitespace), StringComparison.Ordinal);
        }

        int budget = Math.Clamp(maxTrials, 0, 500);
        bool improved;
        do
        {
            improved = false;
            foreach (string candidate in ShrinkCandidates(best, problem, config))
            {
                if (ct.IsCancellationRequested || trials >= budget) return (best, trials);
                if (candidate.Length >= best.Length) continue;
                trials++;
                if (StillFails(candidate))
                {
                    best = candidate;
                    improved = true;
                    log.Line($"  Thu nhỏ phản ví dụ: còn {best.Length} ký tự (thử {trials}/{budget}).");
                    break;
                }
            }
        } while (improved && trials < budget);
        return (best, trials);
    }

    public static DifferentialResult Run(DifferentialSpec spec, Settings st, IJobLog log, CancellationToken ct = default)
    {
        var result = new DifferentialResult();
        if (spec.TestCount < 1 || spec.TestCount > 5000)
        {
            result.Error = "Số test đối chiếu phải trong [1, 5000].";
            return result;
        }
        if (Generator.ValidateFullConfig(spec.Problem, spec.Cfg) is { } configError)
        {
            result.Error = "Cấu hình sinh test nhỏ không hợp lệ: " + configError;
            return result;
        }
        if (spec.ReferencePath.Trim().Length == 0 || spec.CandidatePath.Trim().Length == 0)
        {
            result.Error = "Cần chọn cả lời giải chuẩn và lời giải cần kiểm tra.";
            return result;
        }

        string tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OjGen-Differential"));
        string tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            string inputDir = Path.Combine(tempDir, "inputs");
            string refDir = Path.Combine(tempDir, "reference");
            string candidateDir = Path.Combine(tempDir, "candidate");
            Directory.CreateDirectory(refDir);
            Directory.CreateDirectory(candidateDir);
            var generated = Generator.GenerateInputs(new GenerateRequest
            {
                Problem = spec.Problem,
                Cfg = spec.Cfg,
                TestCount = spec.TestCount,
                Seed = spec.Seed,
                OutDir = inputDir,
                Prefix = "diff",
            }, log, ct);
            if (!generated.Ok)
            {
                result.Cancelled = generated.Cancelled;
                result.Error = generated.Error;
                return result;
            }

            log.Line("Chuẩn bị lời giải tham chiếu…");
            var reference = SolutionRunner.Prepare(spec.ReferencePath, st, refDir, false, log, ct);
            if (!reference.Ok) { result.Error = "Lời giải chuẩn: " + reference.Error; return result; }
            log.Line("Chuẩn bị lời giải cần kiểm tra…");
            var candidate = SolutionRunner.Prepare(spec.CandidatePath, st, candidateDir, false, log, ct);
            if (!candidate.Ok) { result.Error = "Lời giải cần kiểm tra: " + candidate.Error; return result; }

            string refOut = Path.Combine(refDir, "stdout.tmp");
            string refErr = Path.Combine(refDir, "stderr.tmp");
            string candidateOut = Path.Combine(candidateDir, "stdout.tmp");
            string candidateErr = Path.Combine(candidateDir, "stderr.tmp");
            var limits = new RunLimits
            {
                TimeoutMs = st.TimeoutMs,
                MaxOutputBytes = (ulong)st.MaxOutputMB << 20,
                MemoryBytes = (ulong)st.MemoryMB << 20,
            };

            for (int i = 0; i < generated.Inputs.Count; i++)
            {
                if (ct.IsCancellationRequested) { result.Cancelled = true; break; }
                string input = generated.Inputs[i];
                string inputText = FileManager.ReadText(input);
                string name = Path.GetFileNameWithoutExtension(input);
                log.Progress("Đối chiếu lời giải", i + 1, generated.Inputs.Count);
                var refRun = ProcessRunner.Run(reference.Argv, Path.GetFullPath(input), refOut, refErr, refDir, limits, ct, unixLimits: true);
                var candidateRun = ProcessRunner.Run(candidate.Argv, Path.GetFullPath(input), candidateOut, candidateErr, candidateDir, limits, ct, unixLimits: true);
                if (refRun.Status == RunStatus.Cancelled || candidateRun.Status == RunStatus.Cancelled)
                {
                    result.Cancelled = true;
                    break;
                }
                result.Tested++;
                string refText = refRun.Status == RunStatus.Ok ? FileManager.ReadText(refOut) : "";
                string candidateText = candidateRun.Status == RunStatus.Ok ? FileManager.ReadText(candidateOut) : "";
                string reason = "";
                if (refRun.Status != RunStatus.Ok)
                    reason = "Lời giải chuẩn: " + ProcessRunner.StatusName(refRun.Status) + " — " + refRun.Message;
                if (candidateRun.Status != RunStatus.Ok)
                {
                    if (reason.Length > 0) reason += "\n";
                    reason += "Lời giải cần kiểm tra: " + ProcessRunner.StatusName(candidateRun.Status) + " — " + candidateRun.Message;
                }
                if (reason.Length == 0 && !string.Equals(Normalize(refText, spec.IgnoreWhitespace), Normalize(candidateText, spec.IgnoreWhitespace), StringComparison.Ordinal))
                    reason = "Output khác nhau.";
                if (reason.Length > 0)
                {
                    result.Mismatches++;
                    if (result.Failures.Count < 50)
                    {
                        var failure = new DifferentialFailure
                        {
                            TestIndex = i + 1,
                            Input = inputText,
                            ReferenceOutput = refText,
                            CandidateOutput = candidateText,
                            Reason = reason,
                        };
                        if (spec.MinimizeCounterexamples && refRun.Status == RunStatus.Ok && candidateRun.Status == RunStatus.Ok)
                        {
                            var minimized = MinimizeCounterexample(inputText, spec.Problem, spec.Cfg,
                                reference.Argv, candidate.Argv, limits, tempDir, spec.IgnoreWhitespace,
                                spec.MaxMinimizationTrials, log, ct);
                            failure.MinimizationTrials = minimized.Trials;
                            if (minimized.Input.Length < inputText.Length) failure.MinimizedInput = minimized.Input;
                        }
                        result.Failures.Add(failure);
                    }
                    log.Line($"  {name}: KHÁC — {reason.Split('\n')[0]}");
                }
                else log.Line($"  {name}: giống nhau");
            }
            log.Progress("Đối chiếu lời giải", result.Tested, spec.TestCount);
            result.Ok = !result.Cancelled && result.Mismatches == 0 && result.Tested == spec.TestCount;
            return result;
        }
        catch (Exception ex)
        {
            result.Error = "Lỗi khi chạy kiểm thử đối chiếu: " + ex.Message;
            return result;
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
