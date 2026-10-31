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

    public ManualTestSpec Clone() => new()
    {
        Name = Name,
        Input = Input,
        ExpectedOutput = ExpectedOutput,
        GeneratedOutput = GeneratedOutput,
        IsSample = IsSample,
        ConfigName = ConfigName,
        Cfg = Cfg.Clone(),
    };
}
