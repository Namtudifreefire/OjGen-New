// ArrayProblem.cs - Dạng bài mảng một chiều.
//
// Định dạng input:
//   n
//   a1 a2 ... an
using System.Text;

namespace OjGen.Core;

public sealed class ArrayProblem : Problem
{
    public override string Id => "array";
    public override string Title => "Mảng một chiều";
    public override string FormatDescription =>
        "Dòng 1: số nguyên n (minN <= n <= maxN).\n" +
        "Dòng 2: n số nguyên a1 ... an (minVal <= ai <= maxVal), cách nhau một dấu cách.\n" +
        "  distinct = 1: các phần tử đôi một khác nhau.\n" +
        "  sorted   = 0: ngẫu nhiên, 1: không giảm, 2: không tăng.\n" +
        "Ví dụ:\n  5\n  3 -1 4 1 5\n";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minN", "Số phần tử nhỏ nhất (minN)", 1, 1, 1000000),
        ParamSpec.Int("maxN", "Số phần tử lớn nhất (maxN)", 10, 1, 1000000),
        ParamSpec.Int("minVal", "Giá trị phần tử nhỏ nhất", 1, long.MinValue, long.MaxValue),
        ParamSpec.Int("maxVal", "Giá trị phần tử lớn nhất", 100, long.MinValue, long.MaxValue),
        ParamSpec.Bool("distinct", "Các phần tử đôi một khác nhau?", false),
        ParamSpec.Int("sorted", "Sắp xếp (0 = ngẫu nhiên, 1 = tăng dần, 2 = giảm dần)", 0, 0, 2,
            guiLabel: "Sắp xếp",
            options: new[]
            {
                new ParamOption(0, "Ngẫu nhiên"),
                new ParamOption(1, "Không giảm (tăng dần)"),
                new ParamOption(2, "Không tăng (giảm dần)"),
            }),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("minVal") > c.I("maxVal")) return "minVal phải <= maxVal.";
        if (c.B("distinct"))
        {
            ulong span = Util.SpanOf(c.I("minVal"), c.I("maxVal"));
            if (span != ulong.MaxValue && span + 1 < (ulong)c.I("minN"))
                return $"Miền [minVal, maxVal] chỉ có {Util.LL(span + 1)} giá trị, không đủ cho {Util.LL(c.I("minN"))} phần tử khác nhau.";
        }
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        long maxN = c.I("maxN");
        if (c.B("distinct"))
        {
            ulong span = Util.SpanOf(c.I("minVal"), c.I("maxVal"));
            if (span != ulong.MaxValue && (ulong)maxN > span + 1) maxN = (long)(span + 1);
        }
        long n = rng.Range(c.I("minN"), maxN);
        List<long> a;
        if (c.B("distinct"))
        {
            a = DistinctValues(rng, c.I("minVal"), c.I("maxVal"), (int)n);
        }
        else
        {
            a = new List<long>((int)n);
            for (long i = 0; i < n; i++) a.Add(rng.Range(c.I("minVal"), c.I("maxVal")));
        }
        if (c.I("sorted") == 1) a.Sort();
        if (c.I("sorted") == 2) { a.Sort(); a.Reverse(); }
        var sb = new StringBuilder((int)Math.Min(int.MaxValue / 2, n * (long)(Util.MaxDigits(c.I("minVal"), c.I("maxVal")) + 1) + 24));
        Util.AppendLL(sb, n);
        sb.Append('\n');
        for (int i = 0; i < a.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            Util.AppendLL(sb, a[i]);
        }
        sb.Append('\n');
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var L) is { } e) return e;
        if (L.Count != 2) return "Phải có đúng 2 dòng (n và mảng).";
        if (L[0].Length != 1) return "Dòng 1 phải chứa đúng 1 số n.";
        if (ReadIntToken(L[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        if (L[1].Length != n) return $"Dòng 2 có {L[1].Length} số, cần {Util.LL(n)}.";
        var a = new long[n];
        for (int i = 0; i < a.Length; i++)
            if (ReadIntToken(L[1][i], c.I("minVal"), c.I("maxVal"), $"a[{i + 1}]", out a[i]) is { } e2) return e2;
        if (c.I("sorted") == 1 && !IsSorted(a, false)) return "Mảng không tăng dần.";
        if (c.I("sorted") == 2 && !IsSorted(a, true)) return "Mảng không giảm dần.";
        if (c.B("distinct"))
        {
            var b = (long[])a.Clone();
            Array.Sort(b);
            for (int i = 1; i < b.Length; i++)
                if (b[i] == b[i - 1]) return "Mảng có phần tử trùng nhau.";
        }
        return null;
    }

    private static bool IsSorted(long[] a, bool descending)
    {
        for (int i = 1; i < a.Length; i++)
        {
            if (!descending && a[i] < a[i - 1]) return false;
            if (descending && a[i] > a[i - 1]) return false;
        }
        return true;
    }

    public override ulong EstimateMaxBytes(Config c) =>
        (ulong)c.I("maxN") * (Util.MaxDigits(c.I("minVal"), c.I("maxVal")) + 1) + 10;
}
