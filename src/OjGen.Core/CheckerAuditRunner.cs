using System.Text;

namespace OjGen.Core;

public sealed class CheckerAuditResult
{
    public bool Ok { get; init; }
    public bool Cancelled { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public string Error { get; init; } = "";
    public string SelfTestWarning { get; init; } = "";
    public bool MalformedProbeRejected { get; init; }
    public List<CheckerTestResult> Tests { get; init; } = new();

    public string TestDetails => string.Join(Environment.NewLine, Tests.Select(test =>
        $"{test.TestName}: {(test.Accepted ? "ĐẠT" : "LỖI")} ({test.Status}, {test.ElapsedMs} ms)" +
        (test.Detail.Length > 0 ? " — " + test.Detail : "")));
}

public sealed record CheckerTestResult(string TestName, bool Accepted, long ElapsedMs, string Status, string Detail = "");

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
        CancellationToken ct = default, int timeoutMs = 0,
        IReadOnlyList<CheckerContractCase>? contractCases = null)
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
            var testResults = new List<CheckerTestResult>(inputs.Count);
            int limit = Math.Clamp(timeoutMs > 0 ? timeoutMs : settings.TimeoutMs, 1, 3600000);
            var runLimits = new RunLimits
            {
                TimeoutMs = limit,
                MaxOutputBytes = (ulong)Math.Clamp(settings.MaxOutputMB, 1, 4096) << 20,
                MemoryBytes = settings.MemoryMB <= 0 ? 0UL : (ulong)Math.Min(settings.MemoryMB, 1048576) << 20,
            };
            string stdout = Path.Combine(tempDir, "checker.stdout");
            string stderr = Path.Combine(tempDir, "checker.stderr");
            string BuildArg(string value, string input, string contestantOutput, string answer, string testName) => value
                .Replace("{input}", input, StringComparison.Ordinal)
                .Replace("{output}", contestantOutput, StringComparison.Ordinal)
                .Replace("{answer}", answer, StringComparison.Ordinal)
                .Replace("{test}", testName, StringComparison.Ordinal);
            for (int i = 0; i < inputs.Count; i++)
            {
                if (ct.IsCancellationRequested)
                    return new CheckerAuditResult { Passed = passed, Failed = failures.Count, Cancelled = true, Error = "Đã hủy khi chạy checker.", Tests = testResults };
                string input = Path.GetFullPath(inputs[i]);
                string output = Path.GetFullPath(Path.ChangeExtension(input, ".out"));
                if (!File.Exists(output))
                {
                    string missing = $"{Path.GetFileName(input)}: thiếu output .out.";
                    failures.Add(missing);
                    testResults.Add(new CheckerTestResult(Path.GetFileName(input), false, 0, "Thiếu output", "Không tìm thấy file .out"));
                    log.Progress("Chạy checker", i + 1, inputs.Count);
                    continue;
                }
                string testName = Path.GetFileNameWithoutExtension(input);
                var argv = prepared.Argv.Concat(template.Select(value => BuildArg(value, input, output, output, testName))).ToList();
                RunResult run = ProcessRunner.Run(argv, null, stdout, stderr, tempDir, runLimits, ct);
                if (run.Status == RunStatus.Cancelled)
                    return new CheckerAuditResult { Passed = passed, Failed = failures.Count, Cancelled = true, Error = "Đã hủy khi chạy checker.", Tests = testResults };
                if (run.Status == RunStatus.Ok)
                {
                    passed++;
                    testResults.Add(new CheckerTestResult(Path.GetFileName(input), true, run.ElapsedMs, "Chấp nhận"));
                }
                else
                {
                    string detail = string.Join("\n", new[] { FileManager.ReadText(stdout).Trim(), FileManager.ReadText(stderr).Trim() }.Where(s => s.Length > 0));
                    if (detail.Length > 500) detail = detail[..500] + "…";
                    string failure = (run.Message.Length > 0 ? run.Message + (detail.Length > 0 ? "\n" : "") : "") + detail;
                    failures.Add($"{Path.GetFileName(input)}: {ProcessRunner.StatusName(run.Status)} (exit {run.ExitCode}, {run.ElapsedMs} ms)" +
                        (failure.Length > 0 ? " — " + failure : ""));
                    testResults.Add(new CheckerTestResult(Path.GetFileName(input), false, run.ElapsedMs,
                        ProcessRunner.StatusName(run.Status) + " / exit " + run.ExitCode, failure));
                }
                log.Progress("Chạy checker", i + 1, inputs.Count);
            }
            if (contractCases != null)
            {
                foreach (var contractCase in contractCases)
                {
                    if (ct.IsCancellationRequested)
                        return new CheckerAuditResult { Passed = passed, Failed = failures.Count, Cancelled = true, Error = "Đã hủy khi chạy ca hợp đồng checker.", Tests = testResults };
                    string stem = "checker.contract." + testResults.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string inputPath = Path.Combine(tempDir, stem + ".inp");
                    string outputPath = Path.Combine(tempDir, stem + ".out");
                    string answerPath = Path.Combine(tempDir, stem + ".ans");
                    File.WriteAllText(inputPath, contractCase.Input, new UTF8Encoding(false));
                    File.WriteAllText(outputPath, contractCase.ContestantOutput, new UTF8Encoding(false));
                    File.WriteAllText(answerPath, contractCase.Answer, new UTF8Encoding(false));
                    var argv = prepared.Argv.Concat(template.Select(value => BuildArg(value, inputPath, outputPath, answerPath, contractCase.Name))).ToList();
                    RunResult run = ProcessRunner.Run(argv, null, stdout, stderr, tempDir, runLimits, ct);
                    if (run.Status == RunStatus.Cancelled)
                        return new CheckerAuditResult { Passed = passed, Failed = failures.Count, Cancelled = true, Error = "Đã hủy khi chạy ca hợp đồng checker.", Tests = testResults };
                    bool actualAccepted = run.Status == RunStatus.Ok;
                    bool decisive = actualAccepted || (run.Status == RunStatus.RuntimeError && run.ExitCode != 0);
                    bool acceptedAsExpected = decisive && actualAccepted == contractCase.ExpectedAccepted;
                    if (acceptedAsExpected) passed++;
                    else failures.Add($"{contractCase.Name}: mong đợi {(contractCase.ExpectedAccepted ? "chấp nhận" : "từ chối")}, nhận {ProcessRunner.StatusName(run.Status)} (exit {run.ExitCode}).");
                    string detail = acceptedAsExpected ? "Khớp kết quả mong đợi" : $"Mong đợi {(contractCase.ExpectedAccepted ? "chấp nhận" : "từ chối")}; thực tế {ProcessRunner.StatusName(run.Status)} / exit {run.ExitCode}";
                    testResults.Add(new CheckerTestResult(contractCase.Name, acceptedAsExpected, run.ElapsedMs,
                        acceptedAsExpected ? (actualAccepted ? "Chấp nhận đúng" : "Từ chối đúng") : "Sai hợp đồng", detail));
                    log.Progress("Kiểm tra hợp đồng checker", passed + failures.Count, inputs.Count + contractCases.Count);
                }
            }
            string error = failures.Count == 0 ? "" : string.Join("\n", failures.Take(10)) +
                (failures.Count > 10 ? $"\n… còn {failures.Count - 10} test checker lỗi." : "");
            string selfTestWarning = "";
            if (failures.Count == 0 && inputs.Count > 0 && !ct.IsCancellationRequested)
            {
                string firstInput = Path.GetFullPath(inputs[0]);
                string official = Path.GetFullPath(Path.ChangeExtension(firstInput, ".out"));
                string malformedOutput = Path.Combine(tempDir, "checker.malformed.out");
                File.WriteAllText(malformedOutput, "OJGEN_INVALID_OUTPUT\n", new UTF8Encoding(false));
                string testName = Path.GetFileNameWithoutExtension(firstInput);
                var probeArgs = prepared.Argv.Concat(template.Select(value => BuildArg(value, firstInput, malformedOutput, official, testName))).ToList();
                RunResult probe = ProcessRunner.Run(probeArgs, null, stdout, stderr, tempDir, runLimits, ct);
                if (probe.Status == RunStatus.Cancelled)
                    return new CheckerAuditResult { Passed = passed, Failed = failures.Count, Cancelled = true, Error = "Đã hủy khi tự thử checker.", Tests = testResults };
                if (probe.Status == RunStatus.Ok)
                    selfTestWarning = "Checker chấp nhận cả output sai định dạng mẫu; hãy kiểm tra checker và thứ tự {output}/{answer}. Kết quả này chỉ là phép thử sơ bộ.";
                else if (probe.Status == RunStatus.RuntimeError && probe.ExitCode != 0)
                    return new CheckerAuditResult { Ok = true, Passed = passed, Failed = 0, MalformedProbeRejected = true, Tests = testResults };
                else
                    selfTestWarning = $"Không kết luận được phép tự thử checker ({ProcessRunner.StatusName(probe.Status)}); kiểm tra checker thủ công.";
            }
            return new CheckerAuditResult { Ok = failures.Count == 0, Passed = passed, Failed = failures.Count, Error = error, SelfTestWarning = selfTestWarning, Tests = testResults };
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
