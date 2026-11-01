using System.Text.Json;

namespace OjGen.Core;

/// <summary>Explicit, shell-free build/run profile for multi-file solutions.</summary>
public sealed class SolutionRunProfile
{
    public int Version { get; set; } = 1;
    public string BuildExecutable { get; set; } = "";
    public List<string> BuildArguments { get; set; } = new();
    public string RunExecutable { get; set; } = "";
    public List<string> RunArguments { get; set; } = new();
    public string WorkingDirectory { get; set; } = "";

    public static SolutionRunProfile Load(string path)
    {
        var profile = JsonSerializer.Deserialize<SolutionRunProfile>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (profile == null || profile.Version != 1) throw new InvalidDataException("Run profile rỗng hoặc phiên bản không được hỗ trợ.");
        profile.BuildExecutable ??= "";
        profile.RunExecutable ??= "";
        profile.WorkingDirectory ??= "";
        profile.BuildArguments ??= new(); profile.RunArguments ??= new();
        if (string.IsNullOrWhiteSpace(profile.RunExecutable)) throw new InvalidDataException("Run profile cần RunExecutable.");
        if (profile.BuildArguments.Count > 256 || profile.RunArguments.Count > 256 || profile.BuildArguments.Concat(profile.RunArguments).Any(arg => arg == null || arg.Length > 8192))
            throw new InvalidDataException("Run profile có quá nhiều đối số hoặc đối số quá dài.");
        if (profile.BuildExecutable.Length > 1024 || profile.RunExecutable.Length > 1024 || profile.WorkingDirectory.Length > 2048)
            throw new InvalidDataException("Trường đường dẫn trong run profile quá dài.");
        return profile;
    }
}
