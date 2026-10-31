using System.Text.Json;
using System.IO;
using OjGen.Core;

namespace OjGen.Gui;

internal sealed class ProblemSetupResult
{
    public required Problem Problem { get; init; }
    public required Config Config { get; init; }
    public required string Name { get; init; }
    public required string Code { get; init; }
    public required string SolutionPath { get; init; }
}

internal sealed class ProblemProfileFile
{
    public int Version { get; set; } = 1;
    public string ProblemId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string SolutionPath { get; set; } = "";
    public Dictionary<string, long> Ints { get; set; } = new();
    public Dictionary<string, string> Texts { get; set; } = new();

    public static ProblemProfileFile From(Problem problem, Config config, string name, string code, string solution) => new()
    {
        ProblemId = problem.Id,
        Name = name,
        Code = code,
        SolutionPath = solution,
        Ints = new(config.Ints),
        Texts = new(config.Texts),
    };

    public Config ToConfig(Problem problem)
    {
        var config = problem.DefaultConfig();
        foreach (var param in problem.Params)
        {
            if (param.Type == ParamType.Text)
            {
                if (Texts.TryGetValue(param.Key, out var text)) config.SetText(param.Key, text);
            }
            else if (Ints.TryGetValue(param.Key, out var value)) config.Set(param.Key, value);
        }
        return config;
    }

    public static ProblemProfileFile Load(string path)
    {
        var profile = JsonSerializer.Deserialize<ProblemProfileFile>(File.ReadAllText(path));
        if (profile == null || profile.Version != 1) throw new InvalidDataException("Phiên bản hồ sơ không được hỗ trợ.");
        if (Problems.Find(profile.ProblemId) == null) throw new InvalidDataException("Không tìm thấy khuôn sinh input trong hồ sơ.");
        return profile;
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
}
