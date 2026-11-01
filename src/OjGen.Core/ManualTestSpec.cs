namespace OjGen.Core;

/// <summary>Input nhập tay được chèn trước các test sinh tự động.</summary>
public sealed class ManualTestSpec
{
    public string Name { get; set; } = "Test tay";
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    /// <summary>Output do lời giải chuẩn sinh trong lần chạy gần nhất; chỉ dùng trong giao diện.</summary>
    public string GeneratedOutput { get; set; } = "";
    public bool IsSample { get; set; }
    public string ConfigName { get; set; } = "Cấu hình chung";
    public Config Cfg { get; set; } = new();

    /// <summary>Standardizes manually entered input files to LF and a final newline.</summary>
    public static string NormalizeInput(string? input)
    {
        string normalized = (input ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Length > 0 && normalized[^1] != '\n' ? normalized + "\n" : normalized;
    }

    public ManualTestSpec Clone() => new()
    {
        Name = Name,
        Input = NormalizeInput(Input),
        ExpectedOutput = ExpectedOutput,
        GeneratedOutput = GeneratedOutput,
        IsSample = IsSample,
        ConfigName = ConfigName,
        Cfg = Cfg.Clone(),
    };
}
