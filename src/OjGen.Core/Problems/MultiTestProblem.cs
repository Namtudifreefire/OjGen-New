// MultiTestProblem.cs - Nhiều test con, mỗi test là mảng số hoặc xâu.
using System.Text;

namespace OjGen.Core;

public sealed class MultiTestProblem : Problem
{
    private const long MaxItems = 10_000_000;

    public override string Id => "multi_test";
    public override string Title => "Nhiều bộ test (mảng / xâu)";
    public override string FormatDescription =>
        "Dòng 1: số bộ test T. Mỗi bộ gồm một dòng n, sau đó một dòng dữ liệu dài n.\n" +
        "Chọn dữ liệu là dãy số nguyên trong [minVal, maxVal] hoặc xâu trong charset.\n" +
        "Định dạng này thường dùng cho bài có nhiều test con.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("content", "Dữ liệu mỗi test", 0, 0, 1,
            options: new[] { new ParamOption(0, "Mảng số nguyên"), new ParamOption(1, "Xâu ký tự") }),
        ParamSpec.Int("minT", "Số bộ test nhỏ nhất", 2, 1, 10000),
        ParamSpec.Int("maxT", "Số bộ test lớn nhất", 5, 1, 10000),
        ParamSpec.Int("minN", "Độ dài nhỏ nhất", 1, 1, 1000000),
        ParamSpec.Int("maxN", "Độ dài lớn nhất", 10, 1, 1000000),
        ParamSpec.Int("minVal", "Giá trị phần tử nhỏ nhất", 1, long.MinValue, long.MaxValue,
            visibleWhen: c => c.I("content") == 0),
        ParamSpec.Int("maxVal", "Giá trị phần tử lớn nhất", 100, long.MinValue, long.MaxValue,
            visibleWhen: c => c.I("content") == 0),
        ParamSpec.Text("charset", "Tập ký tự của xâu", "lower",
            "lower | upper | letters | digits | alnum | binary | dna | hoặc danh sách ký tự",
            choices: new[] { "lower", "upper", "letters", "digits", "alnum", "binary", "dna" },
            visibleWhen: c => c.I("content") == 1),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minT") > c.I("maxT")) return "minT phải <= maxT.";
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("maxT") > MaxItems / c.I("maxN")) return $"T * n vượt giới hạn {Util.LL(MaxItems)} phần tử.";
        if (c.I("content") == 0 && c.I("minVal") > c.I("maxVal")) return "minVal phải <= maxVal.";
        if (c.I("content") == 1) return StringProblem.ResolveCharset(c.T("charset"), out _);
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        int t = (int)rng.Range(c.I("minT"), c.I("maxT"));
        bool strings = c.I("content") == 1;
        StringProblem.ResolveCharset(c.T("charset"), out string charset);
        var sb = new StringBuilder();
        Util.AppendLL(sb, t);
        sb.Append('\n');
        for (int i = 0; i < t; i++)
        {
            int n = (int)rng.Range(c.I("minN"), c.I("maxN"));
            Util.AppendLL(sb, n);
            sb.Append('\n');
            if (strings)
            {
                for (int j = 0; j < n; j++) sb.Append(charset[(int)rng.Below((ulong)charset.Length)]);
            }
            else
            {
                for (int j = 0; j < n; j++)
                {
                    if (j > 0) sb.Append(' ');
                    Util.AppendLL(sb, rng.Range(c.I("minVal"), c.I("maxVal")));
                }
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } e) return e;
        if (lines[0].Length != 1) return "Dòng 1 phải chứa T.";
        if (ReadIntToken(lines[0][0], c.I("minT"), c.I("maxT"), "T", out long t) is { } e1) return e1;
        if (lines.Count != 1 + 2 * t) return $"Số dòng phải là {Util.LL(1 + 2 * t)}.";
        bool strings = c.I("content") == 1;
        string charset = "";
        if (strings) StringProblem.ResolveCharset(c.T("charset"), out charset);
        for (int i = 0; i < t; i++)
        {
            var nLine = lines[1 + 2 * i];
            if (nLine.Length != 1) return $"Test {i + 1}: dòng độ dài phải có một số n.";
            if (ReadIntToken(nLine[0], c.I("minN"), c.I("maxN"), $"n của test {i + 1}", out long n) is { } e2) return e2;
            var data = lines[2 + 2 * i];
            if (strings)
            {
                if (data.Length != 1 || data[0].Length != n) return $"Test {i + 1}: xâu phải có đúng {Util.LL(n)} ký tự.";
                foreach (char ch in data[0])
                    if (charset.IndexOf(ch) < 0) return $"Test {i + 1}: ký tự '{ch}' không thuộc charset.";
            }
            else
            {
                if (data.Length != n) return $"Test {i + 1}: cần đúng {Util.LL(n)} số.";
                for (int j = 0; j < data.Length; j++)
                    if (ReadIntToken(data[j], c.I("minVal"), c.I("maxVal"), $"a[{j + 1}] của test {i + 1}", out _) is { } e3) return e3;
            }
        }
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong perItem = c.I("content") == 1 ? 1 : Util.MaxDigits(c.I("minVal"), c.I("maxVal")) + 1;
        ulong nDigits = (ulong)Util.LL(c.I("maxN")).Length;
        return (ulong)c.I("maxT") * ((ulong)c.I("maxN") * perItem + 2 * nDigits + 4) + 16;
    }
}
