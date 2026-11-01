using System.Text.Json;

namespace OjGen.Core;

/// <summary>Declarative examples that describe the accept/reject contract of validators and checkers.</summary>
public sealed class ValidationContractSuite
{
    public int Version { get; set; } = 1;
    public List<ValidatorContractCase> ValidatorCases { get; set; } = new();
    public List<CheckerContractCase> CheckerCases { get; set; } = new();

    public static ValidationContractSuite Load(string path)
    {
        var suite = JsonSerializer.Deserialize<ValidationContractSuite>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (suite == null || suite.Version != 1) throw new InvalidDataException("Contract suite rỗng hoặc dùng phiên bản không được hỗ trợ.");
        suite.ValidatorCases ??= new();
        suite.CheckerCases ??= new();
        if (suite.ValidatorCases.Count + suite.CheckerCases.Count > 500)
            throw new InvalidDataException("Contract suite không được có quá 500 ca.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var test in suite.ValidatorCases)
        {
            test.Name = (test.Name ?? "").Trim();
            if (test.Name.Length == 0 || !names.Add("v:" + test.Name)) throw new InvalidDataException("Tên ca validator đang trống hoặc trùng.");
            if (test.Input == null || test.Input.Length > 2_000_000) throw new InvalidDataException($"Input ca '{test.Name}' vượt 2 MB hoặc rỗng.");
        }
        foreach (var test in suite.CheckerCases)
        {
            test.Name = (test.Name ?? "").Trim();
            if (test.Name.Length == 0 || !names.Add("c:" + test.Name)) throw new InvalidDataException("Tên ca checker đang trống hoặc trùng.");
            if (test.Input == null || test.ContestantOutput == null || test.Answer == null ||
                test.Input.Length > 2_000_000 || test.ContestantOutput.Length > 2_000_000 || test.Answer.Length > 2_000_000)
                throw new InvalidDataException($"Dữ liệu ca checker '{test.Name}' vượt 2 MB hoặc bị thiếu.");
        }
        return suite;
    }

    public void Save(string path) => File.WriteAllText(path,
        JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
}

public sealed class ValidatorContractCase
{
    public string Name { get; set; } = "";
    public string Input { get; set; } = "";
    public bool ExpectedAccepted { get; set; }
}

public sealed class CheckerContractCase
{
    public string Name { get; set; } = "";
    public string Input { get; set; } = "";
    public string ContestantOutput { get; set; } = "";
    public string Answer { get; set; } = "";
    public bool ExpectedAccepted { get; set; }
}
