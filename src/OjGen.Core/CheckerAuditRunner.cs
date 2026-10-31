namespace OjGen.Core;

public sealed class CheckerAuditResult
{
    public bool Ok { get; init; }
    public bool Cancelled { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public string Error { get; init; } = "";
}

/// <summary>Runs a checker on official input/output pairs without invoking a shell.</summary>
public static class CheckerAuditRunner
{
    private static readonly HashSet<string> Placeholders = new(StringComparer.Ordinal)
        { "input", "output", "answer", "test" };

    public static bool TryParseArguments(string template, out List<string> arguments, out string error)
    {
        arguments = new List<string>();
        error = "";
        int quotes = template.Count(ch => ch == '"');
        if ((quotes & 1) != 0)
        {
            error = "Thiếu dấu ngoặc kép đóng trong mẫu đối số checker.";
            return false;
        }
        arguments = Util.SplitArgs(template);
        foreach (string argument in arguments)
        {
            for (int i = 0; i < argument.Length; i++)
            {
                if (argument[i] != '{') continue;
                int end = argument.IndexOf('}', i + 1);
                if (end < 0)
                {
                    error = "Placeholder checker chưa đóng dấu } .";
                    return false;
                }
                string name = argument[(i + 1)..end];
                if (!Placeholders.Contains(name))
                {
                    error = $"Placeholder checker không hỗ trợ: {{{name}}}. Dùng {{input}}, {{output}}, {{answer}} hoặc {{test}}.";
                    return false;
                }
                i = end;
            }
        }
        bool hasInput = arguments.Any(arg => arg.Contains("{input}", StringComparison.Ordinal));
        bool hasOutput = arguments.Any(arg => arg.Contains("{output}", StringComparison.Ordinal) || arg.Contains("{answer}", StringComparison.Ordinal));
        if (!hasInput || !hasOutput)
        {
            error = "Mẫu đối số cần có {input} và ít nhất một trong {output}/{answer}.";
            return false;
        }
        return true;
    }

    public static CheckerAuditResult Run(string checkerPath, string argumentsTemplate,
        IReadOnlyList<string> inputs, Settings settings, string tempRoot, IJobLog log,
        CancellationToken ct = default, int timeoutMs = 0)
    {
        if (string.IsNullOrWhiteSpace(checkerPath) || !File.Exists(checkerPath))
            return new CheckerAuditResult { Error = "Không tìm thấy file checker đã chọn." };
        if (!TryParseArguments(argumentsTemplate, out var template, out string parseError))
            return new CheckerAuditResult { Error = parseError };
        if (inputs.Count == 0)
            return new CheckerAuditResult { Error = "Không có input để chạy checker." };

        tempRoot = Path.GetFullPath(tempRoot);
        string tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var prepared = SolutionRunner.Prepare(checkerPath, settings, Path.Combine(tempDir, "build"), false, log, ct);
            if (!prepared.Ok) return new CheckerAuditResult { Error = prepared.Error };

            int passed = 0;
            var failures = new List<string>();
            int limit = Math.Clamp(timeoutMs > 0 ? timeoutMs : settings.TimeoutMs, 1, 3600000);
            var runLimits = new RunLimits
            {
                TimeoutMs = limit,
                MaxOutputBytes = (ulong)Math.Clamp(settings.MaxOutputMB, 1, 4096) << 20,
                MemoryBytes = settings.MemoryMB <= 0 ? 0UL : (ulong)Math.Min(settings.MemoryMB, 1048576) << 20,
            };
            string stdout = Path.Combine(tempDir, "checker.stdout");
            string stderr = Path.Combine(tempDir, "checker.stderr");
            for (int i = 0; i < inputs.Count; i++)
            {
                if (ct.IsCancellationRequested)
                    return new CheckerAuditResult { Passed = passed, Failed = failures.Count, Cancelled = true, Error = "Đã hủy khi chạy checker." };
                string input = Path.GetFullPath(inputs[i]);
                string output = Path.GetFullPath(Path.ChangeExtension(input, ".out"));
                if (!File.Exists(output))
                {
                    failures.Add($"{Path.GetFileName(input)}: thiếu output .out.");
                    continue;
                }
                string testName = Path.GetFileNameWithoutExtension(input);
                string Replace(string value) => value
                    .Replace("{input}", input, StringComparison.Ordinal)
                    .Replace("{output}", output, StringComparison.Ordinal)
                    .Replace("{answer}", output, StringComparison.Ordinal)
                    .Replace("{test}", testName, StringComparison.Ordinal);
                var argv = prepared.Argv.Concat(template.Select(Replace)).ToList();
                RunResult run = ProcessRunner.Run(argv, null, stdout, stderr, tempDir, runLimits, ct);
                if (run.Status == RunStatus.Cancelled)
                    return new CheckerAuditResult { Passed = passed, Failed = failures.Count, Cancelled = true, Error = "Đã hủy khi chạy checker." };
                if (run.Status == RunStatus.Ok)
                {
                    passed++;
                }
                else
                {
                    string detail = string.Join("\n", new[] { FileManager.ReadText(stdout).Trim(), FileManager.ReadText(stderr).Trim() }.Where(s => s.Length > 0));
                    if (detail.Length > 500) detail = detail[..500] + "…";
                    failures.Add($"{Path.GetFileName(input)}: {ProcessRunner.StatusName(run.Status)} (exit {run.ExitCode}, {run.ElapsedMs} ms)" +
                        (run.Message.Length > 0 ? " — " + run.Message : "") + (detail.Length > 0 ? "\n" + detail : ""));
                }
                log.Progress("Chạy checker", i + 1, inputs.Count);
            }
            string error = failures.Count == 0 ? "" : string.Join("\n", failures.Take(10)) +
                (failures.Count > 10 ? $"\n… còn {failures.Count - 10} test checker lỗi." : "");
            return new CheckerAuditResult { Ok = failures.Count == 0, Passed = passed, Failed = failures.Count, Error = error };
        }
        catch (Exception ex)
        {
            return new CheckerAuditResult { Error = "Không chạy được checker: " + ex.Message };
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
