namespace OjGen.Core;

public sealed class SolutionMatrixItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Role { get; set; } = "Ứng viên đúng";
}
public sealed class SolutionMatrixEntry
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public int Compared { get; set; }
    public int Different { get; set; }
    public int Accepted { get; set; }
    public int WrongAnswer { get; set; }
    public int TimeLimitExceeded { get; set; }
    public int RuntimeError { get; set; }
    public int Unavailable { get; set; }
    public string DistinguishingTests { get; set; } = "";
    public string VerdictTests { get; set; } = "";
    public string Status { get; set; } = "";
}
public sealed class SolutionMatrixReport
{
    public string Error { get; set; } = "";
    public List<SolutionMatrixEntry> Entries { get; } = new();
    public string ReferenceName { get; set; } = "";
}

/// <summary>Runs multiple trusted solutions against the same input set and reports tests that separate them.</summary>
public static class SolutionMatrixRunner
{
    public static SolutionMatrixReport Run(string testDirectory, IReadOnlyList<SolutionMatrixItem> solutions, string referenceName,
        Settings settings, string temporaryRoot, IJobLog? log = null, CancellationToken cancellationToken = default)
    {
        log ??= NullLog.Instance;
        var report = new SolutionMatrixReport { ReferenceName = referenceName };
        if (solutions.Count < 2) { report.Error = "Cần ít nhất hai lời giải để so sánh."; return report; }
        if (solutions.Any(item => string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Path)) ||
            solutions.Select(item => item.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != solutions.Count)
        { report.Error = "Mỗi lời giải cần có tên và đường dẫn riêng; tên không được trùng."; return report; }
        var inputs = FileManager.ListFilesWithExt(testDirectory, ".inp");
        if (inputs.Count == 0) { report.Error = "Không tìm thấy file .inp trong thư mục test."; return report; }
        var reference = solutions.FirstOrDefault(item => string.Equals(item.Name, referenceName, StringComparison.OrdinalIgnoreCase));
        if (reference == null || !File.Exists(reference.Path)) { report.Error = "Chọn lời giải chuẩn còn tồn tại."; return report; }
        string root = Path.GetFullPath(temporaryRoot);
        string runRoot = Path.Combine(root, "matrix-" + Guid.NewGuid().ToString("N"));
        var outputs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var statuses = new Dictionary<string, Dictionary<string, RunStatus>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            Directory.CreateDirectory(runRoot);
            for (int solutionIndex = 0; solutionIndex < solutions.Count; solutionIndex++)
            {
                if (cancellationToken.IsCancellationRequested) { report.Error = "Đã hủy."; return report; }
                var item = solutions[solutionIndex];
                if (!File.Exists(item.Path)) { report.Error = $"Không tìm thấy lời giải '{item.Name}': {item.Path}"; return report; }
                string solutionDir = Path.Combine(runRoot, "solution-" + solutionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                string testDir = Path.Combine(solutionDir, "tests");
                Directory.CreateDirectory(testDir);
                var copied = new List<string>(inputs.Count);
                foreach (string input in inputs)
                {
                    string target = Path.Combine(testDir, Path.GetFileName(input));
                    File.Copy(input, target, true);
                    copied.Add(target);
                }
                var prepared = SolutionRunner.Prepare(item.Path, settings, Path.Combine(solutionDir, "build"), true, log, cancellationToken);
                if (!prepared.Ok)
                {
                    report.Error = $"{item.Name}: {prepared.Error}";
                    SolutionRunner.Cleanup(prepared);
                    return report;
                }
                OutputReport run;
                try { run = SolutionRunner.ProduceOutputs(prepared.Argv, copied, settings, testDir, log, cancellationToken, solutionWorkingDirectory: prepared.WorkingDirectory); }
                finally { SolutionRunner.Cleanup(prepared); }
                if (run.Cancelled) { report.Error = "Đã hủy khi chạy ma trận lời giải."; return report; }
                var byStatus = run.Metrics.ToDictionary(metric => metric.Index, metric => metric.Status);
                statuses[item.Name] = copied.Select((input, index) => (input, index))
                    .ToDictionary(pair => Path.GetFileName(pair.input), pair => byStatus.GetValueOrDefault(pair.index + 1, RunStatus.StartFailed), StringComparer.OrdinalIgnoreCase);
                var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string copiedInput in copied)
                {
                    string output = Path.ChangeExtension(copiedInput, ".out");
                    if (File.Exists(output)) byName[Path.GetFileName(copiedInput)] = Normalize(File.ReadAllText(output));
                }
                outputs[item.Name] = byName;
                log.Progress("Chạy ma trận lời giải", solutionIndex + 1, solutions.Count);
            }

            var referenceOutputs = outputs[reference.Name];
            foreach (var item in solutions)
            {
                var current = outputs[item.Name];
                var different = new List<string>();
                var verdictTests = new List<string>();
                int accepted = 0, wrongAnswer = 0, tle = 0, runtimeError = 0, unavailable = 0;
                foreach (string input in inputs)
                {
                    string name = Path.GetFileName(input);
                    string testName = Path.GetFileNameWithoutExtension(name);
                    if (!referenceOutputs.TryGetValue(name, out string? expected)) { unavailable++; verdictTests.Add(testName + ": ref-"); continue; }
                    RunStatus runStatus = statuses[item.Name].GetValueOrDefault(name, RunStatus.StartFailed);
                    if (runStatus == RunStatus.Timeout) { tle++; different.Add(testName); verdictTests.Add(testName + ": TLE"); continue; }
                    if (runStatus != RunStatus.Ok || !current.TryGetValue(name, out string? actual))
                    { runtimeError++; different.Add(testName); verdictTests.Add(testName + ": RE"); continue; }
                    if (string.Equals(expected, actual, StringComparison.Ordinal)) { accepted++; verdictTests.Add(testName + ": AC"); }
                    else { wrongAnswer++; different.Add(testName); verdictTests.Add(testName + ": WA"); }
                }
                bool isReference = string.Equals(item.Name, reference.Name, StringComparison.OrdinalIgnoreCase);
                bool caught = different.Count > 0;
                report.Entries.Add(new SolutionMatrixEntry
                {
                    Name = item.Name, Role = isReference ? "Lời giải chuẩn" : item.Role,
                    Compared = inputs.Count, Different = different.Count,
                    Accepted = accepted, WrongAnswer = wrongAnswer, TimeLimitExceeded = tle, RuntimeError = runtimeError, Unavailable = unavailable,
                    DistinguishingTests = string.Join(", ", different.Take(24)) + (different.Count > 24 ? $" … +{different.Count - 24}" : ""),
                    VerdictTests = string.Join(", ", verdictTests),
                    Status = isReference ? "Mốc so sánh" : item.Role == "Lời giải sai" ? (caught ? "Bị bộ test bắt" : "Chưa bị bộ test bắt") : (caught ? "Khác lời giải chuẩn" : "Khớp lời giải chuẩn"),
                });
            }
        }
        catch (Exception ex) { report.Error = "Lỗi khi so sánh lời giải: " + ex.Message; }
        finally
        {
            try
            {
                string fullTarget = Path.GetFullPath(runRoot);
                if (string.Equals(Path.GetDirectoryName(fullTarget), root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullTarget)) Directory.Delete(fullTarget, true);
            }
            catch { }
        }
        return report;
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
