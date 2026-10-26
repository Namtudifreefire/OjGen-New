// Queries.cs - Dạng bài mảng kết hợp nhiều truy vấn.
//
// Định dạng input:
//   n q
//   a1 a2 ... an
//   q dòng, mỗi dòng là một truy vấn:
//     1 i x   -> cập nhật: gán a[i] = x        (1 <= i <= n, minVal <= x <= maxVal)
//     2 l r   -> hỏi trên đoạn [l, r]           (1 <= l <= r <= n)
using System.Text;

namespace OjGen.Core;

public sealed class QueriesProblem : Problem
{
    public override string Id => "queries";
    public override string Title => "Mảng và truy vấn";
    public override string FormatDescription =>
        "Dòng 1: n q.\n" +
        "Dòng 2: n số nguyên a1 ... an (minVal <= ai <= maxVal).\n" +
        "q dòng tiếp theo, mỗi dòng một truy vấn:\n" +
        "  1 i x : gán a[i] = x   (1 <= i <= n, minVal <= x <= maxVal)\n" +
        "  2 l r : truy vấn đoạn [l, r]   (1 <= l <= r <= n)\n" +
        "Tỷ lệ truy vấn loại 1 do updatePct quy định (0..100). Nếu updatePct < 100,\n" +
        "mỗi test có ít nhất một truy vấn loại 2.\n" +
        "Ví dụ:\n  5 3\n  1 2 3 4 5\n  2 1 3\n  1 2 10\n  2 2 5\n";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minN", "Số phần tử nhỏ nhất (minN)", 1, 1, 1000000),
        ParamSpec.Int("maxN", "Số phần tử lớn nhất (maxN)", 10, 1, 1000000),
        ParamSpec.Int("minVal", "Giá trị phần tử nhỏ nhất", 1, long.MinValue, long.MaxValue),
        ParamSpec.Int("maxVal", "Giá trị phần tử lớn nhất", 100, long.MinValue, long.MaxValue),
        ParamSpec.Int("minQ", "Số truy vấn nhỏ nhất (minQ)", 1, 1, 1000000),
        ParamSpec.Int("maxQ", "Số truy vấn lớn nhất (maxQ)", 10, 1, 1000000),
        ParamSpec.Int("updatePct", "Tỷ lệ % truy vấn cập nhật (loại 1)", 30, 0, 100,
            "0 = chỉ có truy vấn hỏi đoạn, 100 = chỉ có cập nhật"),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minN") > c.I("maxN")) return "minN phải <= maxN.";
        if (c.I("minVal") > c.I("maxVal")) return "minVal phải <= maxVal.";
        if (c.I("minQ") > c.I("maxQ")) return "minQ phải <= maxQ.";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        long n = rng.Range(c.I("minN"), c.I("maxN"));
        long q = rng.Range(c.I("minQ"), c.I("maxQ"));
        long lo = c.I("minVal"), hi = c.I("maxVal");
        int pct = (int)c.I("updatePct");
        var sb = new StringBuilder();
        Util.AppendLL(sb, n);
        sb.Append(' ');
        Util.AppendLL(sb, q);
        sb.Append('\n');
        for (long i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            Util.AppendLL(sb, rng.Range(lo, hi));
        }
        sb.Append('\n');
        bool hasRangeQuery = false;
        for (long k = 0; k < q; k++)
        {
            bool update = rng.Chance(pct);
            // Bảo đảm có ít nhất một truy vấn loại 2 khi updatePct < 100.
            if (k == q - 1 && !hasRangeQuery && pct < 100) update = false;
            if (update)
            {
                sb.Append("1 ");
                Util.AppendLL(sb, rng.Range(1, n));
                sb.Append(' ');
                Util.AppendLL(sb, rng.Range(lo, hi));
            }
            else
            {
                hasRangeQuery = true;
                long l = rng.Range(1, n);
                long r = rng.Range(1, n);
                if (l > r) (l, r) = (r, l);
                sb.Append("2 ");
                Util.AppendLL(sb, l);
                sb.Append(' ');
                Util.AppendLL(sb, r);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var L) is { } e) return e;
        if (L.Count < 2 || L[0].Length != 2) return "Dòng 1 phải chứa n q.";
        if (ReadIntToken(L[0][0], c.I("minN"), c.I("maxN"), "n", out long n) is { } e1) return e1;
        if (ReadIntToken(L[0][1], c.I("minQ"), c.I("maxQ"), "q", out long q) is { } e2) return e2;
        if (L.Count != 2 + q) return $"Số dòng là {L.Count}, cần {Util.LL(2 + q)}.";
        if (L[1].Length != n) return "Dòng 2 phải có đúng n số.";
        for (int i = 0; i < L[1].Length; i++)
            if (ReadIntToken(L[1][i], c.I("minVal"), c.I("maxVal"), $"a[{i + 1}]", out _) is { } e3) return e3;
        bool hasRange = false;
        for (long k = 0; k < q; k++)
        {
            var t = L[(int)(2 + k)];
            string where = $"Truy vấn {k + 1}";
            if (t.Length != 3) return where + ": phải có đúng 3 số.";
            if (ReadIntToken(t[0], 1, 2, where + " (loại)", out long type) is { } e4) return e4;
            long x, y;
            if (type == 1)
            {
                if (ReadIntToken(t[1], 1, n, where + " (i)", out x) is { } e5) return e5;
                if (ReadIntToken(t[2], c.I("minVal"), c.I("maxVal"), where + " (x)", out y) is { } e6) return e6;
            }
            else
            {
                hasRange = true;
                if (ReadIntToken(t[1], 1, n, where + " (l)", out x) is { } e7) return e7;
                if (ReadIntToken(t[2], 1, n, where + " (r)", out y) is { } e8) return e8;
                if (x > y) return where + ": l > r.";
            }
        }
        if (c.I("updatePct") < 100 && !hasRange) return "Không có truy vấn loại 2 nào.";
        if (c.I("updatePct") == 0)
        {
            for (long k = 0; k < q; k++)
                if (L[(int)(2 + k)][0] == "1") return "updatePct = 0 nhưng có truy vấn loại 1.";
        }
        if (c.I("updatePct") == 100 && hasRange) return "updatePct = 100 nhưng có truy vấn loại 2.";
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong d = Util.MaxDigits(c.I("minVal"), c.I("maxVal"));
        ulong dn = (ulong)Util.LL(c.I("maxN")).Length;
        return (ulong)c.I("maxN") * (d + 1) + (ulong)c.I("maxQ") * (4 + dn + (d > dn ? d : dn)) + 32;
    }
}
