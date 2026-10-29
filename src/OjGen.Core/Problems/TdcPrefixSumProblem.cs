// QBOJ TDC (Tổng Dãy Con): truy vấn tổng các phần tử dương trong đoạn.
// Vì dãy rỗng cũng là dãy con hợp lệ, phần tử âm có thể bỏ qua.
using System.Text;

namespace OjGen.Core;

public sealed class TdcPrefixSumProblem : Problem
{
    private const long MaxCount = 100_000;
    private const long MaxAbsValue = 10_000_000;

    public override string Id => "prefix_subsequence_range";
    public override string Title => "Tiền tố: tổng dãy con theo truy vấn (QBOJ TDC)";
    public override string MainCategory => "Dãy và truy vấn";
    public override string FormatDescription =>
        "Dòng 1: n q (1 <= n, q <= 100000).\n" +
        "Dòng 2: n số nguyên a1 ... an (|ai| <= 10000000).\n" +
        "q dòng tiếp theo: mỗi dòng có l r (1 <= l <= r <= n).\n" +
        "TDC yêu cầu tổng lớn nhất của một dãy con trong đoạn [l, r]. Dãy rỗng được phép, " +
        "nên đáp án bằng tổng các phần tử dương trong đoạn; có thể xử lý bằng mảng tiền tố.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minN", "Số phần tử nhỏ nhất (minN)", 1, 1, MaxCount),
        ParamSpec.Int("maxN", "Số phần tử lớn nhất (maxN)", 1000, 1, MaxCount),
        ParamSpec.Int("minQ", "Số truy vấn nhỏ nhất (minQ)", 1, 1, MaxCount),
        ParamSpec.Int("maxQ", "Số truy vấn lớn nhất (maxQ)", 1000, 1, MaxCount),
        ParamSpec.Int("minVal", "Giá trị phần tử nhỏ nhất", -MaxAbsValue, -MaxAbsValue, MaxAbsValue),
        ParamSpec.Int("maxVal", "Giá trị phần tử lớn nhất", MaxAbsValue, -MaxAbsValue, MaxAbsValue),
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
        sb.Append(' ');
        Util.AppendLL(sb, q);
        sb.Append('\n');

        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            Util.AppendLL(sb, rng.Range(c.I("minVal"), c.I("maxVal")));
        }
        sb.Append('\n');

        for (int i = 0; i < q; i++)
        {
            long l, r;
            if (i == 0) { l = 1; r = n; }
            else if (i == 1) { l = 1; r = 1; }
            else if (i == 2) { l = n; r = n; }
            else
            {
                l = rng.Range(1, n);
                r = rng.Range(l, n);
            }
            Util.AppendLL(sb, l);
            sb.Append(' ');
            Util.AppendLL(sb, r);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } e) return e;
        if (lines.Count < 2 || lines[0].Length != 2) return "Dòng 1 phải chứa đúng n q.";
        if (ReadIntToken(lines[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        if (ReadIntToken(lines[0][1], c.I("minQ"), c.I("maxQ"), "q", out long q) is { } e2) return e2;
        if (lines.Count != q + 2) return $"Cần đúng {Util.LL(q)} dòng truy vấn sau dòng mảng.";
        if (lines[1].Length != n) return $"Dòng 2 phải có đúng {Util.LL(n)} số.";
        for (int i = 0; i < lines[1].Length; i++)
            if (ReadIntToken(lines[1][i], c.I("minVal"), c.I("maxVal"), $"a[{i + 1}]", out _) is { } e3) return e3;
        for (int i = 0; i < q; i++)
        {
            var row = lines[i + 2];
            if (row.Length != 2) return $"Truy vấn {i + 1} phải chứa đúng hai số l r.";
            if (ReadIntToken(row[0], 1, n, $"l của truy vấn {i + 1}", out long l) is { } e4) return e4;
            if (ReadIntToken(row[1], 1, n, $"r của truy vấn {i + 1}", out long r) is { } e5) return e5;
            if (l > r) return $"Truy vấn {i + 1} phải thỏa l <= r.";
        }
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong n = (ulong)c.I("maxN"), q = (ulong)c.I("maxQ");
        ulong valueDigits = (ulong)Util.MaxDigits(c.I("minVal"), c.I("maxVal"));
        ulong indexDigits = (ulong)Util.MaxDigits(1, c.I("maxN"));
        return 2 * (ulong)Util.MaxDigits(1, c.I("maxN")) + 2
             + n * (valueDigits + 1) + 1
             + q * (2 * indexDigits + 2) + 16;
    }
}
