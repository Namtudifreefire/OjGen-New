// PairListProblem.cs - Danh sách cặp số hoặc các đoạn [l, r].
using System.Text;

namespace OjGen.Core;

public sealed class PairListProblem : Problem
{
    public override string Id => "pairs";
    public override string Title => "Danh sách cặp / đoạn";
    public override string FormatDescription =>
        "Dòng 1: n. Tiếp theo n dòng, mỗi dòng có một cặp x y.\n" +
        "Nếu chọn dữ liệu đoạn, mọi cặp thỏa x <= y.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minN", "Số cặp nhỏ nhất", 1, 1, 1000000),
        ParamSpec.Int("maxN", "Số cặp lớn nhất", 20, 1, 1000000),
        ParamSpec.Int("minVal", "Giá trị nhỏ nhất", 1, long.MinValue, long.MaxValue),
        ParamSpec.Int("maxVal", "Giá trị lớn nhất", 100, long.MinValue, long.MaxValue),
        ParamSpec.Bool("intervals", "Cặp là đoạn [l, r] (l <= r)?", false),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("minVal") > c.I("maxVal")) return "minVal phải <= maxVal.";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        long n = rng.Range(c.I("minN"), c.I("maxN"));
        var sb = new StringBuilder();
        Util.AppendLL(sb, n); sb.Append('\n');
        for (long i = 0; i < n; i++)
        {
            long x = rng.Range(c.I("minVal"), c.I("maxVal"));
            long y = c.B("intervals") ? rng.Range(x, c.I("maxVal")) : rng.Range(c.I("minVal"), c.I("maxVal"));
            Util.AppendLL(sb, x); sb.Append(' '); Util.AppendLL(sb, y); sb.Append('\n');
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } e) return e;
        if (lines[0].Length != 1) return "Dòng 1 phải chứa n.";
        if (ReadIntToken(lines[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        if (lines.Count != n + 1) return $"Số dòng phải là {Util.LL(n + 1)}.";
        for (int i = 0; i < n; i++)
        {
            var pair = lines[i + 1];
            if (pair.Length != 2) return $"Cặp {i + 1} phải có đúng hai số.";
            if (ReadIntToken(pair[0], c.I("minVal"), c.I("maxVal"), $"Cặp {i + 1} (x)", out long x) is { } e2) return e2;
            if (ReadIntToken(pair[1], c.I("minVal"), c.I("maxVal"), $"Cặp {i + 1} (y)", out long y) is { } e3) return e3;
            if (c.B("intervals") && x > y) return $"Đoạn {i + 1} có l > r.";
        }
        return null;
    }

    public override ulong EstimateMaxBytes(Config c) =>
        (ulong)c.I("maxN") * (2 * Util.MaxDigits(c.I("minVal"), c.I("maxVal")) + 2) + 24;
}
