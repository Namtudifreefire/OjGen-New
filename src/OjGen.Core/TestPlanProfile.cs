using System.Text.Json;

namespace OjGen.Core;

/// <summary>Reusable, versioned JSON representation of a plan of test groups.</summary>
public sealed class TestPlanProfile
{
    public int Version { get; set; } = 1;
    public string ProblemId { get; set; } = "";
    public List<string> RequiredCoverageTags { get; set; } = new();
    public List<TestPlanGroupProfile> Groups { get; set; } = new();

    public static TestPlanProfile From(Problem problem, IReadOnlyList<TestGroupSpec> groups,
        IReadOnlyList<string>? requiredCoverageTags = null) => new()
    {
        ProblemId = problem.Id,
        RequiredCoverageTags = requiredCoverageTags?.ToList() ?? new List<string>(),
        Groups = groups.Select(group => new TestPlanGroupProfile
        {
            Name = group.Name,
            TestCount = group.TestCount,
            Seed = group.Seed,
            CoverageTags = group.CoverageTags?.ToList() ?? new List<string>(),
            Points = group.Points,
            DependsOn = group.DependsOn?.ToList() ?? new List<string>(),
            Ints = new Dictionary<string, long>(group.Cfg.Ints, StringComparer.OrdinalIgnoreCase),
            Texts = new Dictionary<string, string>(group.Cfg.Texts, StringComparer.OrdinalIgnoreCase),
        }).ToList(),
    };

    public static TestPlanProfile Load(string path)
    {
        var profile = JsonSerializer.Deserialize<TestPlanProfile>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (profile == null) throw new InvalidDataException("File kế hoạch rỗng hoặc sai định dạng.");
        if (profile.Version != 1) throw new InvalidDataException($"Không hỗ trợ phiên bản kế hoạch {profile.Version}.");
        if (string.IsNullOrWhiteSpace(profile.ProblemId)) throw new InvalidDataException("Kế hoạch chưa có ProblemId.");
        if (profile.Groups == null || profile.Groups.Count == 0 || profile.Groups.Count > Generator.MaxGroups)
            throw new InvalidDataException($"Số nhóm phải trong [1, {Generator.MaxGroups}].");
        if (TestCoverageTags.Parse(string.Join(',', profile.RequiredCoverageTags ?? new()), out var requiredTags) is { } tagsError)
            throw new InvalidDataException(tagsError);
        profile.RequiredCoverageTags = requiredTags;
        return profile;
    }

    public List<TestGroupSpec> ToGroups(Problem problem)
    {
        if (Version != 1) throw new InvalidDataException($"Không hỗ trợ phiên bản kế hoạch {Version}.");
        if (!string.Equals(ProblemId, problem.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Kế hoạch dành cho khuôn '{ProblemId}', hiện đang chọn '{problem.Id}'.");
        if (Groups == null || Groups.Count == 0 || Groups.Count > Generator.MaxGroups)
            throw new InvalidDataException($"Số nhóm phải trong [1, {Generator.MaxGroups}].");

        var specs = problem.Params.ToDictionary(param => param.Key, StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<TestGroupSpec>(Groups.Count);
        long total = 0;
        bool hasPoints = Groups.Any(group => group.Points > 0);
        int totalPoints = 0;
        foreach (TestPlanGroupProfile saved in Groups)
        {
            string name = (saved.Name ?? "").Trim();
            if (name.Length == 0 || !names.Add(name)) throw new InvalidDataException("Tên nhóm bị trống hoặc trùng nhau.");
            if (saved.TestCount < 1 || saved.TestCount > Generator.MaxTests) throw new InvalidDataException($"Số test của nhóm '{name}' không hợp lệ.");
            if (saved.Points is < 0 or > 100) throw new InvalidDataException($"Điểm nhóm '{name}' phải trong [0, 100].");
            totalPoints += saved.Points;
            total += saved.TestCount;
            if (total > Generator.MaxTests) throw new InvalidDataException($"Tổng số test vượt quá {Generator.MaxTests}.");
            var config = problem.DefaultConfig();
            foreach (var pair in saved.Ints ?? new Dictionary<string, long>())
            {
                if (!specs.TryGetValue(pair.Key, out var spec) || spec.Type == ParamType.Text)
                    throw new InvalidDataException($"Tham số số nguyên không được hỗ trợ: {pair.Key}.");
                config.Set(spec.Key, pair.Value);
            }
            foreach (var pair in saved.Texts ?? new Dictionary<string, string>())
            {
                if (!specs.TryGetValue(pair.Key, out var spec) || spec.Type != ParamType.Text)
                    throw new InvalidDataException($"Tham số văn bản không được hỗ trợ: {pair.Key}.");
                config.SetText(spec.Key, pair.Value ?? "");
            }
            if (Generator.ValidateFullConfig(problem, config) is { } error)
                throw new InvalidDataException($"Nhóm '{name}' có cấu hình sai: {error}");
            if (TestCoverageTags.Parse(string.Join(',', saved.CoverageTags ?? new()), out var groupTags) is { } groupTagsError)
                throw new InvalidDataException($"Nhóm '{name}': {groupTagsError}");
            result.Add(new TestGroupSpec { Name = name, TestCount = saved.TestCount, Seed = saved.Seed, Cfg = config, CoverageTags = groupTags,
                Points = saved.Points, DependsOn = (saved.DependsOn ?? new()).Select(item => (item ?? "").Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList() });
        }
        var namesByGroup = result.ToDictionary(group => group.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var group in result)
        {
            if (group.DependsOn.Any(name => !namesByGroup.ContainsKey(name)))
                throw new InvalidDataException($"Nhóm '{group.Name}' phụ thuộc vào tên nhóm không tồn tại.");
            if (group.DependsOn.Any(name => string.Equals(name, group.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Nhóm '{group.Name}' không thể phụ thuộc vào chính nó.");
        }
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool HasCycle(TestGroupSpec group)
        {
            if (active.Contains(group.Name)) return true;
            if (!visited.Add(group.Name)) return false;
            active.Add(group.Name);
            foreach (string dependency in group.DependsOn)
                if (HasCycle(namesByGroup[dependency])) return true;
            active.Remove(group.Name);
            return false;
        }
        if (result.Any(HasCycle)) throw new InvalidDataException("Phụ thuộc giữa các nhóm có vòng lặp.");
        if (hasPoints && totalPoints != 100) throw new InvalidDataException("Khi gán điểm, tổng điểm các nhóm phải bằng 100.");
        if (TestCoverageTags.Parse(string.Join(',', RequiredCoverageTags ?? new()), out var normalizedRequired) is { } requiredError)
            throw new InvalidDataException(requiredError);
        RequiredCoverageTags = normalizedRequired;
        return result;
    }

    public int TotalTestCount => Groups?.Sum(group => group.TestCount) ?? 0;

    public void Save(string path) => AtomicFile.WriteAllText(path,
        JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
}

public sealed class TestPlanGroupProfile
{
    public string Name { get; set; } = "";
    public int TestCount { get; set; }
    public ulong? Seed { get; set; }
    public List<string> CoverageTags { get; set; } = new();
    public int Points { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public Dictionary<string, long> Ints { get; set; } = new();
    public Dictionary<string, string> Texts { get; set; } = new();
}
