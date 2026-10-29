// PrefixSumRangeProblem.cs - Mảng và truy vấn tổng đoạn theo định dạng QBOJ sumarr.
// Input: n; mảng a[1..n]; q; q dòng x y. Output do lời giải chuẩn xử lý.
using System.Text;

namespace OjGen.Core;

public sealed class PrefixSumRangeProblem : Problem
{
    private const long MaxCount = 100_000;
    private const long MaxValue = 1_000_000_000;

    public override string Id => "prefix_sum_range";
    public override string Title => "Tiền tố: truy vấn tổng đoạn (QBOJ sumarr)";
    public override string MainCategory => "Dãy và truy vấn";
    public override string FormatDescription =>
        "Dòng 1: n.\n" +
        "Dòng 2: n số nguyên a1 ... an.\n" +
        "Dòng 3: q.\n" +
        "q dòng tiếp theo: mỗi dòng chứa x y (1 <= x <= y <= n).\n" +
        "Đây là khuôn input của QBOJ sumarr; lời giải chuẩn in tổng a[x..y] cho mỗi truy vấn.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minN", "Số phần tử nhỏ nhất", 1, 1, MaxCount),
        ParamSpec.Int("maxN", "Số phần tử lớn nhất", 1000, 1, MaxCount),
        ParamSpec.Int("minQ", "Số truy vấn nhỏ nhất", 1, 1, MaxCount),
        ParamSpec.Int("maxQ", "Số truy vấn lớn nhất", 1000, 1, MaxCount),
        ParamSpec.Int("minVal", "Giá trị phần tử nhỏ nhất", 0, 0, MaxValue),
        ParamSpec.Int("maxVal", "Giá trị phần tử lớn nhất", MaxValue, 0, MaxValue),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("minQ") > c.I("maxQ")) return "minQ phải <= maxQ.";
        if (c.I("minVal") > c.I("maxVal")) return "minVal phải <= maxVal.";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        int n = (int)rng.Range(c.I("minN"), c.I("maxN"));
        int q = (int)rng.Range(c.I("minQ"), c.I("maxQ"));
        var sb = new StringBuilder((int)Math.Min(int.MaxValue, EstimateMaxBytes(c)));
        Util.AppendLL(sb, n);
        sb.Append('\n');
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            Util.AppendLL(sb, rng.Range(c.I("minVal"), c.I("maxVal")));
        }
        sb.Append('\n');
        Util.AppendLL(sb, q);
        sb.Append('\n');
        for (int i = 0; i < q; i++)
        {
            long x, y;
            if (i == 0) { x = 1; y = n; }
            else if (i == 1) { x = 1; y = 1; }
            else if (i == 2) { x = n; y = n; }
            else
            {
                x = rng.Range(1, n);
                y = rng.Range(x, n);
            }
            Util.AppendLL(sb, x);
            sb.Append(' ');
            Util.AppendLL(sb, y);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } e) return e;
        if (lines.Count < 3) return "Cần có dòng n, dòng mảng và dòng q.";
        if (lines[0].Length != 1) return "Dòng 1 phải chứa đúng một số n.";
        if (ReadIntToken(lines[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        if (lines[1].Length != n) return $"Dòng 2 phải có đúng {Util.LL(n)} số.";
        for (int i = 0; i < lines[1].Length; i++)
            if (ReadIntToken(lines[1][i], c.I("minVal"), c.I("maxVal"), $"a[{i + 1}]", out _) is { } e2) return e2;
        if (lines[2].Length != 1) return "Dòng 3 phải chứa đúng một số q.";
        if (ReadIntToken(lines[2][0], c.I("minQ"), c.I("maxQ"), "q", out long q) is { } e3) return e3;
        if (lines.Count != q + 3) return $"Phải có đúng {Util.LL(q)} dòng truy vấn.";
        for (int i = 0; i < q; i++)
        {
            var row = lines[i + 3];
            if (row.Length != 2) return $"Truy vấn {i + 1} phải chứa đúng hai số x y.";
            if (ReadIntToken(row[0], 1, n, $"x của truy vấn {i + 1}", out long x) is { } e4) return e4;
            if (ReadIntToken(row[1], 1, n, $"y của truy vấn {i + 1}", out long y) is { } e5) return e5;
            if (x > y) return $"Truy vấn {i + 1} phải thỏa x <= y.";
        }
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong n = (ulong)c.I("maxN"), q = (ulong)c.I("maxQ");
        ulong valueDigits = (ulong)Util.MaxDigits(c.I("minVal"), c.I("maxVal"));
        ulong indexDigits = (ulong)Util.MaxDigits(1, c.I("maxN"));
        return (ulong)Util.MaxDigits(1, c.I("maxN")) + 1
             + n * (valueDigits + 1) + 1
             + (ulong)Util.MaxDigits(1, c.I("maxQ")) + 1
             + q * (2 * indexDigits + 2) + 16;
    }
}
