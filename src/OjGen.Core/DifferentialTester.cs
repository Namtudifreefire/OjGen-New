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
}

public sealed class DifferentialFailure
{
    public int TestIndex { get; set; }
    public string Input { get; set; } = "";
    public string ReferenceOutput { get; set; } = "";
    public string CandidateOutput { get; set; } = "";
    public string Reason { get; set; } = "";
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
            config.Set(param.Key, Math.Clamp(20, param.Lo, param.Hi));
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
                        result.Failures.Add(new DifferentialFailure
                        {
                            TestIndex = i + 1,
                            Input = inputText,
                            ReferenceOutput = refText,
                            CandidateOutput = candidateText,
                            Reason = reason,
                        });
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
