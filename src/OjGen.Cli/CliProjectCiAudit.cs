using System.Text.Json;
using OjGen.Core;

namespace OjGen.Cli;

internal sealed record CiProjectAuditOutcome(int ExitCode, string Text, object Json, string Error = "");

/// <summary>Runs a headless release audit from a saved GUI .ojproject without writing audit files into the source project.</summary>
internal static class CliProjectCiAudit
{
    private static JsonElement Property(JsonElement source, params string[] names)
    {
        foreach (string name in names)
            if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value)) return value;
        return default;
    }
    private static string String(JsonElement source, params string[] names)
    {
        var value = Property(source, names);
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    }
    private static int Int(JsonElement source, params string[] names)
    {
        var value = Property(source, names);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : 0;
    }
    private static ulong ULong(JsonElement source, params string[] names)
    {
        var value = Property(source, names);
        return value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out ulong number) ? number : 0;
    }
    private static Dictionary<string, long> LongMap(JsonElement source)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (source.ValueKind != JsonValueKind.Object) return result;
        foreach (var property in source.EnumerateObject()) if (property.Value.TryGetInt64(out long value)) result[property.Name] = value;
        return result;
    }
    private static Dictionary<string, string> StringMap(JsonElement source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source.ValueKind != JsonValueKind.Object) return result;
        foreach (var property in source.EnumerateObject()) if (property.Value.ValueKind == JsonValueKind.String) result[property.Name] = property.Value.GetString() ?? "";
        return result;
    }
    private static string Resolve(string root, string path) => string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));

    public static CiProjectAuditOutcome Run(string projectPath, IJobLog log)
    {
        string fullProject = Path.GetFullPath(projectPath);
        if (!File.Exists(fullProject)) return new(2, "", new { exitCode = 2, passed = false }, "Không tìm thấy project: " + fullProject);
        string sourceRoot = Path.GetDirectoryName(fullProject)!;
        string tempRoot = Path.Combine(Path.GetTempPath(), "OjGen-CI-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(fullProject));
            JsonElement root = json.RootElement;
            if (Int(root, "Version") != 1) throw new InvalidDataException("Không hỗ trợ phiên bản .ojproject.");
            string problemId = String(root, "ProblemId");
            Problem problem = Problems.Find(problemId) ?? throw new InvalidDataException("Không tìm thấy khuôn bài: " + problemId);
            string testSource = Resolve(sourceRoot, String(root, "LastTestDirectory"));
            if (testSource.Length == 0 || !Directory.Exists(testSource)) throw new DirectoryNotFoundException("Project chưa có thư mục test đã sinh hoặc đường dẫn LastTestDirectory không còn tồn tại.");

            var config = problem.DefaultConfig();
            JsonElement lastInts = Property(root, "LastGeneratedInts");
            JsonElement lastTexts = Property(root, "LastGeneratedTexts");
            var ints = lastInts.ValueKind == JsonValueKind.Object ? LongMap(lastInts) : LongMap(Property(root, "Ints"));
            var texts = lastTexts.ValueKind == JsonValueKind.Object ? StringMap(lastTexts) : StringMap(Property(root, "Texts"));
            foreach (var parameter in problem.Params)
            {
                if (parameter.Type == ParamType.Text && texts.TryGetValue(parameter.Key, out string? text)) config.SetText(parameter.Key, text);
                else if (parameter.Type != ParamType.Text && ints.TryGetValue(parameter.Key, out long value)) config.Set(parameter.Key, value);
            }
            if (Generator.ValidateFullConfig(problem, config) is { } configError) throw new InvalidDataException("Cấu hình project không hợp lệ: " + configError);

            int expected = Int(root, "LastGeneratedCount");
            JsonElement planJson = Property(root, "LastGeneratedPlan");
            if (planJson.ValueKind != JsonValueKind.Object || Property(planJson, "Groups").ValueKind != JsonValueKind.Array || Property(planJson, "Groups").GetArrayLength() == 0)
                planJson = Property(root, "TestPlan");
            TestPlanProfile? plan = planJson.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<TestPlanProfile>(planJson.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
            var groups = plan?.Groups?.Count > 0 ? plan.ToGroups(problem) : new List<TestGroupSpec>();
            var requiredTags = plan?.RequiredCoverageTags?.ToList() ?? new List<string>();
            if (requiredTags.Count == 0 && Property(root, "LastGeneratedCoverageTags") is { ValueKind: JsonValueKind.Array } savedTags)
                requiredTags = savedTags.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString() ?? "").Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (expected < 1) expected = FileManager.ListFilesWithExt(testSource, ".inp").Count;
            int manualCount = Property(root, "LastGeneratedManualTests") is { ValueKind: JsonValueKind.Array } manualArray ? manualArray.GetArrayLength() : 0;
            int generatedCount = Math.Max(0, expected - manualCount);
            if (groups.Count > 0 && groups.Sum(group => group.TestCount) != generatedCount)
                throw new InvalidDataException("Số test của kế hoạch đã lưu không khớp số test sinh trong project.");

            Directory.CreateDirectory(tempRoot);
            string tempTests = Path.Combine(tempRoot, "tests");
            CopyDirectoryBounded(testSource, tempTests, 50000, 2L * 1024 * 1024 * 1024);
            string statementPath = "";
            var resourceErrors = new List<string>();
            var resourceWarnings = new List<string>();
            JsonElement statement = Property(root, "Statement");
            if (statement.ValueKind == JsonValueKind.Object)
            {
                statementPath = Path.Combine(tempRoot, "problem.ojdraft.json");
                File.WriteAllText(statementPath, statement.GetRawText(), new System.Text.UTF8Encoding(false));
                var resourceText = string.Join("\n", "# " + String(statement, "Name"), String(statement, "Description"), String(statement, "Input"),
                    String(statement, "Output"), String(statement, "Constraints"), String(statement, "Examples"));
                var resources = StatementResourceManager.CheckContent(resourceText, sourceRoot);
                resourceErrors.AddRange(resources.Missing.Select(item => "Thiếu statement asset: " + item));
                resourceErrors.AddRange(resources.Unsafe.Select(item => "Đường dẫn statement asset không an toàn: " + item));
                resourceWarnings.AddRange(resources.Remote.Select(item => "Asset từ URL ngoài không được kiểm tra khi CI chạy offline: " + item));
            }

            JsonElement package = Property(root, "Package");
            string reference = Resolve(sourceRoot, String(root, "LastGeneratedSolutionPath"));
            if (reference.Length == 0) reference = Resolve(sourceRoot, String(root, "SolutionPath"));
            string checker = Resolve(sourceRoot, String(package, "CheckerPath"));
            string validator = Resolve(sourceRoot, String(package, "ValidatorPath"));
            string contractSuite = Resolve(sourceRoot, String(package, "ContractSuitePath"));
            string candidate = Resolve(sourceRoot, String(package, "CandidateSolution"));
            ulong seed = ULong(root, "LastGeneratedSeed");
            var generatedSpec = new JobSpec
            {
                Problem = problem, Cfg = config.Clone(), TestCount = expected, GeneratedTestCount = generatedCount, Seed = seed,
                OutDir = tempTests, Groups = groups, RequiredCoverageTags = requiredTags,
            };
            Settings settings = Settings.Load(null, out _);
            var options = new CliAuditOptions
            {
                Problem = problem, Config = config, Settings = settings, TestDirectory = tempTests, ExpectedTestCount = expected,
                ReferenceSolution = reference, ValidatorPath = validator, CheckerPath = checker,
                CheckerArguments = String(package, "CheckerArguments") is { Length: > 0 } checkerArgs ? checkerArgs : "{input} {output} {answer}",
                CheckerTimeoutMs = Int(package, "CheckerTimeoutMs"), ContractSuitePath = contractSuite, CandidateSolution = candidate,
                StatementPath = statementPath, Seed = seed, DifferentialSeed = seed, GeneratedSpec = generatedSpec, RequiredCoverageTags = requiredTags,
            };
            CliAuditReport audit = CliReleaseAudit.Run(options, log);
            foreach (string item in resourceErrors) audit.Add(CliAuditLevel.Error, "Statement resource", item);
            foreach (string item in resourceWarnings) audit.Add(CliAuditLevel.Warning, "Statement resource", item);
            int exit = audit.HasErrors ? 2 : audit.WarningCount > 0 ? 1 : 0;
            var report = new
            {
                exitCode = exit, passed = !audit.HasErrors, warningCount = audit.WarningCount,
                items = audit.Items.Select(item => new { level = item.Level.ToString(), item.Title, item.Detail }).ToList(),
                sourceProject = fullProject, sourceTests = testSource,
            };
            return new(exit, audit.ToText(), report);
        }
        catch (Exception ex)
        {
            return new(2, "", new { exitCode = 2, passed = false, error = ex.Message }, ex.Message);
        }
        finally
        {
            try
            {
                string fullTemp = Path.GetFullPath(tempRoot);
                string tempBase = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (fullTemp.StartsWith(tempBase, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullTemp)) Directory.Delete(fullTemp, true);
            }
            catch { }
        }
    }

    private static void CopyDirectoryBounded(string source, string destination, int maxFiles, long maxBytes)
    {
        Directory.CreateDirectory(destination);
        int count = 0;
        long bytes = 0;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            count++;
            bytes = checked(bytes + info.Length);
            if (count > maxFiles || bytes > maxBytes) throw new InvalidDataException("Bộ test CI vượt giới hạn sao chép an toàn (50.000 file / 2 GiB).");
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
