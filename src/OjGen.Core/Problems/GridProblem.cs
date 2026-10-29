// GridProblem.cs - Lưới ký tự, dùng cho mê cung và các bài dạng ô lưới.
using System.Text;

namespace OjGen.Core;

public sealed class GridProblem : Problem
{
    private const long MaxCells = 10_000_000;

    public override string Id => "grid";
    public override string Title => "Lưới ký tự";
    public override string FormatDescription =>
        "Dòng 1: n m. Tiếp theo n dòng, mỗi dòng là một xâu dài m.\n" +
        "Ký tự lấy từ charset; có thể bật tùy chọn để đặt đúng một ô bắt đầu S và một ô đích T.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minN", "Số hàng nhỏ nhất", 2, 1, 1000000),
        ParamSpec.Int("maxN", "Số hàng lớn nhất", 10, 1, 1000000),
        ParamSpec.Int("minM", "Số cột nhỏ nhất", 2, 1, 1000000),
        ParamSpec.Int("maxM", "Số cột lớn nhất", 10, 1, 1000000),
        ParamSpec.Text("charset", "Tập ký tự", ".#",
            "lower | upper | letters | digits | alnum | binary | dna | hoặc danh sách ký tự",
            choices: new[] { ".#", "01", "binary", "dna", "lower", "upper", "set:S.T#" }),
        ParamSpec.Bool("markers", "Đặt đúng một S và một T?", false,
            "Bật tùy chọn này thì charset cần có cả S và T.")
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("minM") > c.I("maxM")) return "minM phải <= maxM.";
        if (c.I("maxN") > MaxCells / c.I("maxM")) return $"Lưới vượt giới hạn {Util.LL(MaxCells)} ô.";
        if (c.B("markers") && c.I("minN") * c.I("minM") < 2) return "Lưới cần ít nhất 2 ô để đặt S và T.";
        if (StringProblem.ResolveCharset(c.T("charset"), out string charset) is { } e) return e;
        if (c.B("markers") && (!charset.Contains('S') || !charset.Contains('T')))
            return "Khi đặt S/T, charset phải chứa cả hai ký tự S và T (vd set:S.T#).";
        if (c.B("markers") && !charset.Any(ch => ch != 'S' && ch != 'T'))
            return "Charset cần thêm ít nhất một ký tự nền ngoài S và T (vd set:S.T#).";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        int n = (int)rng.Range(c.I("minN"), c.I("maxN"));
        int m = (int)rng.Range(c.I("minM"), c.I("maxM"));
        StringProblem.ResolveCharset(c.T("charset"), out string charset);
        string fillCharset = c.B("markers") ? new string(charset.Where(ch => ch != 'S' && ch != 'T').ToArray()) : charset;
        var cells = new char[n * m];
        for (int i = 0; i < cells.Length; i++) cells[i] = fillCharset[(int)rng.Below((ulong)fillCharset.Length)];
        if (c.B("markers"))
        {
            int start = (int)rng.Below((ulong)cells.Length);
            int target = (int)rng.Below((ulong)(cells.Length - 1));
            if (target >= start) target++;
            cells[start] = 'S';
            cells[target] = 'T';
        }
        var sb = new StringBuilder(cells.Length + n * 2 + 24);
        Util.AppendLL(sb, n); sb.Append(' '); Util.AppendLL(sb, m); sb.Append('\n');
        for (int i = 0; i < n; i++)
        {
            sb.Append(cells, i * m, m);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } e) return e;
        if (lines[0].Length != 2) return "Dòng 1 phải chứa n m.";
        if (ReadIntToken(lines[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        if (ReadIntToken(lines[0][1], c.I("minM"), c.I("maxM"), "m", out long m) is { } e2) return e2;
        if (lines.Count != n + 1) return $"Số dòng lưới phải là {Util.LL(n)}.";
        StringProblem.ResolveCharset(c.T("charset"), out string charset);
        int starts = 0, targets = 0;
        for (int i = 0; i < n; i++)
        {
            if (lines[i + 1].Length != 1 || lines[i + 1][0].Length != m)
                return $"Dòng lưới {i + 1} phải là một xâu dài {Util.LL(m)}.";
            foreach (char ch in lines[i + 1][0])
            {
                if (charset.IndexOf(ch) < 0) return $"Ký tự '{ch}' không thuộc charset.";
                if (ch == 'S') starts++;
                if (ch == 'T') targets++;
            }
        }
        if (c.B("markers") && (starts != 1 || targets != 1)) return "Lưới phải có đúng một S và một T.";
        return null;
    }

    public override ulong EstimateMaxBytes(Config c) =>
        (ulong)c.I("maxN") * ((ulong)c.I("maxM") + 1) + (ulong)Util.LL(c.I("maxN")).Length + (ulong)Util.LL(c.I("maxM")).Length + 4;
}
