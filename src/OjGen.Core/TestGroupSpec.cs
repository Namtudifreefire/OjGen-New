namespace OjGen.Core;

/// <summary>Một nhóm test dùng chung số lượng, seed và cấu hình sinh.</summary>
public sealed class TestGroupSpec
{
    public string Name { get; set; } = "Nhóm test";
    public int TestCount { get; set; } = 10;
    /// <summary>Null = OjGen tạo seed ổn định từ seed chính của bộ test.</summary>
    public ulong? Seed { get; set; }
    public Config Cfg { get; set; } = new();
    public List<string> CoverageTags { get; set; } = new();
    /// <summary>Optional judge score for this group. Zero means not assigned.</summary>
    public int Points { get; set; }
    /// <summary>Names of groups that must pass before this group is scored.</summary>
    public List<string> DependsOn { get; set; } = new();

    public TestGroupSpec Clone() => new()
    {
        Name = Name,
        TestCount = TestCount,
        Seed = Seed,
        Cfg = Cfg.Clone(),
        CoverageTags = CoverageTags?.ToList() ?? new List<string>(),
        Points = Points,
        DependsOn = DependsOn?.ToList() ?? new List<string>(),
    };
}
