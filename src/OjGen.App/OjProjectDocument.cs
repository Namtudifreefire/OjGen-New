using System.IO;
using System.Text.Json;
using OjGen.Core;

namespace OjGen.Gui;

/// <summary>Portable authoring workspace. Paths are stored as supplied so projects can reference files outside the project folder.</summary>
internal sealed class OjProjectDocument
{
    public int Version { get; set; } = 1;
    public DateTimeOffset SavedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ProblemId { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string ProblemName { get; set; } = "";
    public string ProblemCode { get; set; } = "";
    public int TestCount { get; set; } = 10;
    public string Seed { get; set; } = "";
    public string SolutionPath { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public bool Zip { get; set; } = true;
    public bool ZipIncomplete { get; set; }
    public bool Overwrite { get; set; }
    public bool Rebuild { get; set; }
    public bool Resume { get; set; }
    public Dictionary<string, long> Ints { get; set; } = new();
    public Dictionary<string, string> Texts { get; set; } = new();
    public TestPlanProfile TestPlan { get; set; } = new();
    public List<ProjectManualTest> ManualTests { get; set; } = new();
    public ProblemAuthoringDocument? Statement { get; set; }
    public ProjectPackageOptions Package { get; set; } = new();
    public string LastTestDirectory { get; set; } = "";
    public int LastGeneratedCount { get; set; }
    public ulong LastGeneratedSeed { get; set; }
    public string LastGeneratedProblemId { get; set; } = "";
    public string LastGeneratedSolutionPath { get; set; } = "";
    public Dictionary<string, long> LastGeneratedInts { get; set; } = new();
    public Dictionary<string, string> LastGeneratedTexts { get; set; } = new();
    public TestPlanProfile LastGeneratedPlan { get; set; } = new();
    public List<ProjectManualTest> LastGeneratedManualTests { get; set; } = new();
    public List<string> LastGeneratedCoverageTags { get; set; } = new();

    public void Save(string path)
    {
        SavedUtc = DateTimeOffset.UtcNow;
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        AtomicFile.WriteAllText(full, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
    }

    public static OjProjectDocument Load(string path)
    {
        OjProjectDocument? document = JsonSerializer.Deserialize<OjProjectDocument>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (document == null || document.Version != 1)
            throw new InvalidDataException("File dự án trống hoặc dùng phiên bản không được hỗ trợ.");
        if (Problems.Find(document.ProblemId) == null)
            throw new InvalidDataException($"Không tìm thấy khuôn bài '{document.ProblemId}' trong phiên bản OjGen hiện tại.");
        document.TestPlan ??= new TestPlanProfile();
        document.ManualTests ??= new List<ProjectManualTest>();
        document.Package ??= new ProjectPackageOptions();
        document.Ints ??= new Dictionary<string, long>();
        document.Texts ??= new Dictionary<string, string>();
        document.LastGeneratedInts ??= new Dictionary<string, long>();
        document.LastGeneratedTexts ??= new Dictionary<string, string>();
        document.LastGeneratedPlan ??= new TestPlanProfile();
        document.LastGeneratedManualTests ??= new List<ProjectManualTest>();
        document.LastGeneratedCoverageTags ??= new List<string>();
        if (document.Statement != null && document.Statement.Version != 1)
            throw new InvalidDataException("Bản đề trong dự án dùng phiên bản không được hỗ trợ.");
        return document;
    }
}

internal sealed class ProjectManualTest
{
    public string Name { get; set; } = "Test tay";
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public string GeneratedOutput { get; set; } = "";
    public bool IsSample { get; set; }
    public string ConfigName { get; set; } = "Cấu hình chung";
    public Dictionary<string, long> Ints { get; set; } = new();
    public Dictionary<string, string> Texts { get; set; } = new();

    public static ProjectManualTest From(ManualTestSpec test) => new()
    {
        Name = test.Name, Input = test.Input, ExpectedOutput = test.ExpectedOutput,
        GeneratedOutput = test.GeneratedOutput, IsSample = test.IsSample,
        ConfigName = test.ConfigName, Ints = new(test.Cfg.Ints), Texts = new(test.Cfg.Texts),
    };

    public ManualTestSpec ToSpec(Problem problem)
    {
        var config = problem.DefaultConfig();
        foreach (var parameter in problem.Params)
        {
            if (parameter.Type == ParamType.Text)
            {
                if (Texts.TryGetValue(parameter.Key, out string? value)) config.SetText(parameter.Key, value);
            }
            else if (Ints.TryGetValue(parameter.Key, out long value)) config.Set(parameter.Key, value);
        }
        return new ManualTestSpec
        {
            Name = Name, Input = Input, ExpectedOutput = ExpectedOutput, GeneratedOutput = GeneratedOutput,
            IsSample = IsSample, ConfigName = ConfigName, Cfg = config,
        };
    }
}

internal sealed class ProjectPackageOptions
{
    public string ValidatorPath { get; set; } = "";
    public string CheckerPath { get; set; } = "";
    public string CheckerArguments { get; set; } = "{input} {output} {answer}";
    public int CheckerTimeoutMs { get; set; }
    public string ContractSuitePath { get; set; } = "";
    public string CandidateSolution { get; set; } = "";
    public string ReferenceSolution { get; set; } = "";
    public string ParentDirectory { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string ExportFormat { get; set; } = "OjGen";
    public bool IncludeTests { get; set; } = true;
    public bool CreateZip { get; set; } = true;
    public bool IncludeReferenceSolution { get; set; }

    public void Capture(ProblemPackageOptions source)
    {
        ValidatorPath = source.ValidatorPath;
        CheckerPath = source.CheckerPath;
        CheckerArguments = source.CheckerArguments;
        CheckerTimeoutMs = source.CheckerTimeoutMs;
        ContractSuitePath = source.ContractSuitePath;
        CandidateSolution = source.CandidateSolution;
        ReferenceSolution = source.ReferenceSolution;
        ParentDirectory = source.ParentDirectory;
        PackageName = source.PackageName;
        ExportFormat = source.ExportFormat;
        IncludeTests = source.IncludeTests;
        CreateZip = source.CreateZip;
        IncludeReferenceSolution = source.IncludeReferenceSolution;
    }
}
