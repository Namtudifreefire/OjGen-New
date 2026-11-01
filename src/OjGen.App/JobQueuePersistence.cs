using System.IO;
using System.Text.Json;
using OjGen.Core;

namespace OjGen.Gui;

internal sealed class QueuedJobEntry
{
    public JobSpec Spec { get; set; } = null!;
    public ExistingPolicy Policy { get; set; }
    public override string ToString() => $"{(Spec.ProblemName.Length > 0 ? Spec.ProblemName : Spec.Problem.Title)} · {Spec.TestCount} test · seed {Util.LL(Spec.Seed ?? 0)}";
}

internal sealed class PersistedQueue
{
    public int Version { get; set; } = 1;
    public List<PersistedQueueEntry> Items { get; set; } = new();
}

internal sealed class PersistedQueueEntry
{
    public string ProblemId { get; set; } = "";
    public string ProblemName { get; set; } = "";
    public string ProblemCode { get; set; } = "";
    public int TestCount { get; set; }
    public ulong Seed { get; set; }
    public string SolutionPath { get; set; } = "";
    public string OutDir { get; set; } = "";
    public bool Zip { get; set; }
    public bool ZipIncomplete { get; set; }
    public bool ForceRebuild { get; set; }
    public bool ResumeGeneration { get; set; }
    public string Policy { get; set; } = "NewDir";
    public Dictionary<string, long> Ints { get; set; } = new();
    public Dictionary<string, string> Texts { get; set; } = new();
    public TestPlanProfile TestPlan { get; set; } = new();
    public List<ProjectManualTest> ManualTests { get; set; } = new();
}

internal static class JobQueuePersistence
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OjGen", "queue.json");
    public static void Save(string path, IEnumerable<QueuedJobEntry> entries)
    {
        var document = new PersistedQueue();
        foreach (var entry in entries)
        {
            var spec = entry.Spec;
            var groups = spec.Groups.Count > 0 ? spec.Groups : new List<TestGroupSpec> { new() { Name = "Tất cả", TestCount = Math.Max(1, spec.TestCount - spec.ManualTests.Count), Cfg = spec.Cfg.Clone() } };
            document.Items.Add(new PersistedQueueEntry
            {
                ProblemId = spec.Problem.Id, ProblemName = spec.ProblemName, ProblemCode = spec.ProblemCode, TestCount = spec.TestCount,
                Seed = spec.Seed ?? 0, SolutionPath = spec.SolutionPath, OutDir = spec.OutDir, Zip = spec.Zip == ZipMode.Yes,
                ZipIncomplete = spec.ZipIncomplete, ForceRebuild = spec.ForceRebuild, ResumeGeneration = spec.ResumeGeneration,
                Policy = entry.Policy.ToString(), Ints = new(spec.Cfg.Ints), Texts = new(spec.Cfg.Texts),
                TestPlan = TestPlanProfile.From(spec.Problem, groups, spec.RequiredCoverageTags),
                ManualTests = spec.ManualTests.Select(ProjectManualTest.From).ToList(),
            });
        }
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        AtomicFile.WriteAllText(full, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
    }

    public static List<QueuedJobEntry> Load(string path)
    {
        if (!File.Exists(path)) return new();
        var document = JsonSerializer.Deserialize<PersistedQueue>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (document == null || document.Version != 1 || document.Items == null || document.Items.Count > 200)
            throw new InvalidDataException("Hàng đợi rỗng, quá lớn hoặc dùng phiên bản không được hỗ trợ.");
        var result = new List<QueuedJobEntry>();
        foreach (var item in document.Items)
        {
            if (item == null) throw new InvalidDataException("Hàng đợi chứa một mục rỗng.");
            item.ProblemId ??= "";
            item.ProblemName ??= "";
            item.ProblemCode ??= "";
            item.SolutionPath ??= "";
            item.OutDir ??= "";
            var problem = Problems.Find(item.ProblemId) ?? throw new InvalidDataException($"Không tìm thấy khuôn '{item.ProblemId}' trong hàng đợi.");
            var config = problem.DefaultConfig();
            foreach (var pair in item.Ints ?? new()) if (problem.Params.Any(p => p.Key.Equals(pair.Key, StringComparison.OrdinalIgnoreCase) && p.Type != ParamType.Text)) config.Set(pair.Key, pair.Value);
            foreach (var pair in item.Texts ?? new()) if (problem.Params.Any(p => p.Key.Equals(pair.Key, StringComparison.OrdinalIgnoreCase) && p.Type == ParamType.Text)) config.SetText(pair.Key, pair.Value);
            item.TestPlan ??= new TestPlanProfile();
            item.TestPlan.Groups ??= new();
            item.TestPlan.RequiredCoverageTags ??= new();
            item.ManualTests ??= new();
            item.Ints ??= new();
            item.Texts ??= new();
            if (item.TestPlan.Groups.Any(group => group == null) || item.ManualTests.Any(test => test == null))
                throw new InvalidDataException("Hàng đợi chứa nhóm hoặc test mẫu rỗng.");
            if (item.TestPlan.Groups.Count == 0)
                item.TestPlan = TestPlanProfile.From(problem, new[] { new TestGroupSpec { Name = "Tất cả", TestCount = Math.Max(1, item.TestCount - (item.ManualTests?.Count ?? 0)), Cfg = config.Clone() } });
            var groups = item.TestPlan.ToGroups(problem);
            var policy = Enum.TryParse<ExistingPolicy>(item.Policy, true, out var parsed) ? parsed : ExistingPolicy.NewDir;
            var spec = new JobSpec
            {
                Problem = problem, Cfg = config, TestCount = item.TestCount, Seed = item.Seed, SolutionPath = item.SolutionPath,
                OutDir = item.OutDir, Zip = item.Zip ? ZipMode.Yes : ZipMode.No, ZipIncomplete = item.ZipIncomplete,
                ForceRebuild = item.ForceRebuild, ResumeGeneration = item.ResumeGeneration, ProblemName = item.ProblemName,
                ProblemCode = item.ProblemCode, Groups = groups, RequiredCoverageTags = item.TestPlan.RequiredCoverageTags.ToList(),
                ManualTests = (item.ManualTests ?? new()).Select(test => test.ToSpec(problem)).ToList(),
            };
            if (spec.TestCount < 1 || spec.TestCount > Generator.MaxTests) throw new InvalidDataException("Một mục trong hàng đợi có số test không hợp lệ.");
            result.Add(new QueuedJobEntry { Spec = spec, Policy = policy });
        }
        return result;
    }
}
