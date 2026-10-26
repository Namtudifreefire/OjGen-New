// Config.cs - Mô tả tham số và cấu hình của một dạng bài.
namespace OjGen.Core;

public enum ParamType { Int, Bool, Text }

/// <summary>Một lựa chọn có nhãn cho tham số số nguyên (vd sorted: 0 = ngẫu nhiên, 1 = tăng dần...).</summary>
public sealed record ParamOption(long Value, string Label);

/// <summary>
/// Mô tả một tham số: dùng cho dòng lệnh (--key value), form trên giao diện và kiểm tra miền giá trị.
/// </summary>
public sealed class ParamSpec
{
    public string Key { get; init; } = "";
    /// <summary>Nhãn đầy đủ (ghi vào file thông tin và dùng ở dòng lệnh).</summary>
    public string Label { get; init; } = "";
    /// <summary>Nhãn ngắn trên giao diện; rỗng = dùng Label.</summary>
    public string GuiLabel { get; init; } = "";
    public ParamType Type { get; init; } = ParamType.Int;
    public long Def { get; init; }
    public long Lo { get; init; }
    public long Hi { get; init; }
    public string DefText { get; init; } = "";
    public string Help { get; init; } = "";
    /// <summary>Lựa chọn có nhãn cho tham số Int (giao diện hiện ComboBox).</summary>
    public IReadOnlyList<ParamOption>? Options { get; init; }
    /// <summary>Giá trị gợi ý cho tham số Text (giao diện hiện ComboBox có thể gõ tay).</summary>
    public IReadOnlyList<string>? Choices { get; init; }
    /// <summary>Điều kiện hiển thị trên giao diện (null = luôn hiện).</summary>
    public Func<Config, bool>? VisibleWhen { get; init; }
    /// <summary>Tham số Text nhập nhiều dòng.</summary>
    public bool Multiline { get; init; }

    public string DisplayLabel => GuiLabel.Length > 0 ? GuiLabel : Label;

    public static ParamSpec Int(string key, string label, long def, long lo, long hi, string help = "",
        string guiLabel = "", IReadOnlyList<ParamOption>? options = null, Func<Config, bool>? visibleWhen = null)
        => new() { Key = key, Label = label, GuiLabel = guiLabel, Type = ParamType.Int, Def = def, Lo = lo, Hi = hi, Help = help, Options = options, VisibleWhen = visibleWhen };

    public static ParamSpec Bool(string key, string label, bool def, string help = "", Func<Config, bool>? visibleWhen = null)
        => new() { Key = key, Label = label, Type = ParamType.Bool, Def = def ? 1 : 0, Lo = 0, Hi = 1, Help = help, VisibleWhen = visibleWhen };

    public static ParamSpec Text(string key, string label, string defText, string help = "",
        IReadOnlyList<string>? choices = null, bool multiline = false, Func<Config, bool>? visibleWhen = null)
        => new() { Key = key, Label = label, Type = ParamType.Text, DefText = defText, Help = help, Choices = choices, Multiline = multiline, VisibleWhen = visibleWhen };
}

/// <summary>Cấu hình cụ thể của một lần sinh test (giá trị các tham số).</summary>
public sealed class Config
{
    public Dictionary<string, long> Ints { get; } = new();
    public Dictionary<string, string> Texts { get; } = new();

    public long I(string key) => Ints.TryGetValue(key, out var v) ? v : 0;
    public bool B(string key) => I(key) != 0;
    public string T(string key) => Texts.TryGetValue(key, out var v) ? v : "";
    public void Set(string key, long value) => Ints[key] = value;
    public void SetText(string key, string value) => Texts[key] = value;

    public Config Clone()
    {
        var c = new Config();
        foreach (var kv in Ints) c.Ints[kv.Key] = kv.Value;
        foreach (var kv in Texts) c.Texts[kv.Key] = kv.Value;
        return c;
    }
}
