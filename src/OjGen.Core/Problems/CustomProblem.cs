// CustomProblem.cs - Dãy n số nguyên với giới hạn do người dùng cấu hình.
//
// Hai chế độ giới hạn:
//   mode = 1 : mọi số nằm trong [min, max] (giới hạn chung)
//   mode = 2 : số thứ i nằm trong [lo_i, hi_i] (giới hạn riêng từng vị trí),
//              lưu trong tham số bounds dạng "lo1:hi1,lo2:hi2,..."
//              (GUI cho phép nhập mỗi vị trí một dòng; chấp nhận dấu ',' ';' hoặc xuống dòng)
//
// Định dạng input (mặc định header = 0, layout = 1):
//   [n]                (chỉ khi header = 1)
//   x1 x2 ... xn       (layout = 1: cùng một dòng)
//   hoặc mỗi số một dòng (layout = 2)
using System.Text;

namespace OjGen.Core;

public sealed class CustomProblem : Problem
{
    public const long MaxCount = 1000000;

    public override string Id => "custom";
    public override string Title => "Tùy chỉnh: dãy số nguyên, giới hạn chung hoặc riêng từng vị trí";
    public override string FormatDescription =>
        "Mỗi test gồm n số nguyên (n cố định cho mọi test).\n" +
        "  mode = 1: mọi số trong [min, max].\n" +
        "  mode = 2: số thứ i trong [lo_i, hi_i]; CLI: --bounds \"1:10,50:100,-20:20\".\n" +
        "  header = 1: ghi n ở dòng đầu.  layout = 1: các số trên một dòng; layout = 2: mỗi số một dòng.\n" +
        "Ví dụ (n = 3, mode = 2, header = 0, layout = 1):\n  7 93 -15\n";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("n", "Số lượng số nguyên trong mỗi test (n)", 10, 1, MaxCount),
        ParamSpec.Int("mode", "Chế độ giới hạn (1 = chung, 2 = riêng từng vị trí)", 1, 1, 2,
            guiLabel: "Chế độ giới hạn",
            options: new[]
            {
                new ParamOption(1, "Giới hạn chung cho mọi số"),
                new ParamOption(2, "Giới hạn riêng từng vị trí"),
            }),
        ParamSpec.Int("min", "Giá trị nhỏ nhất (chế độ 1)", 1, long.MinValue, long.MaxValue,
            visibleWhen: c => c.I("mode") == 1),
        ParamSpec.Int("max", "Giá trị lớn nhất (chế độ 1)", 100, long.MinValue, long.MaxValue,
            visibleWhen: c => c.I("mode") == 1),
        ParamSpec.Text("bounds", "Giới hạn riêng (chế độ 2), dạng lo1:hi1,lo2:hi2,...", "",
            "Mỗi vị trí một cặp lo:hi, ngăn cách bằng dấu phẩy hoặc xuống dòng. Số cặp phải bằng n.",
            multiline: true, visibleWhen: c => c.I("mode") == 2),
        ParamSpec.Bool("header", "Ghi n ở dòng đầu?", false),
        ParamSpec.Int("layout", "Bố cục (1 = cùng một dòng, 2 = mỗi số một dòng)", 1, 1, 2,
            guiLabel: "Bố cục",
            options: new[]
            {
                new ParamOption(1, "Các số trên cùng một dòng"),
                new ParamOption(2, "Mỗi số một dòng"),
            }),
    };

    /// <summary>Phân tích chuỗi bounds. Trả về null nếu hợp lệ.</summary>
    public static string? ParseBounds(string text, out List<(long Lo, long Hi)> result)
    {
        result = new List<(long, long)>();
        string t = text.Trim();
        if (t.Length == 0) return "Chưa có giới hạn riêng (bounds) cho từng vị trí.";
        var parts = t.Split(new[] { ',', ';', '\n' });
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            // Dấu ':' phân cách lo và hi (lo có thể âm, vd -20:20 hoặc -9:-1).
            int colon = p.IndexOf(':');
            string where = $"bounds vị trí {i + 1}";
            if (colon < 0) return $"{where}: cần dạng lo:hi, nhận được '{p}'.";
            if (!Util.ParseLL(p[..colon], out long lo) || !Util.ParseLL(p[(colon + 1)..], out long hi))
                return $"{where}: giá trị không hợp lệ hoặc vượt phạm vi long long ('{p}').";
            if (lo > hi) return $"{where}: giá trị nhỏ nhất {Util.LL(lo)} > lớn nhất {Util.LL(hi)}.";
            result.Add((lo, hi));
        }
        return null;
    }

    public static string BoundsToText(IReadOnlyList<(long Lo, long Hi)> b)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < b.Count; i++)
        {
            if (i > 0) sb.Append(',');
            Util.AppendLL(sb, b[i].Lo);
            sb.Append(':');
            Util.AppendLL(sb, b[i].Hi);
        }
        return sb.ToString();
    }

    public override void FinalizeCliConfig(Config c, ISet<string> keys)
    {
        if (!keys.Contains("bounds")) return;
        // Có --bounds thì mặc định là chế độ 2; lỗi cú pháp sẽ được ValidateConfig báo.
        if (!keys.Contains("mode")) c.Set("mode", 2);
        if (ParseBounds(c.T("bounds"), out var b) == null && !keys.Contains("n")) c.Set("n", b.Count);
    }

    public override void AdjustConfig(Config c, string changedKey)
    {
        // Dán/sửa bounds ở chế độ 2 thì n tự bằng số vị trí (giống --bounds ở dòng lệnh).
        if (changedKey == "bounds" && c.I("mode") == 2 && ParseBounds(c.T("bounds"), out var b) == null
            && b.Count >= 1 && b.Count <= MaxCount)
            c.Set("n", b.Count);
    }

    public override string? ValidateConfig(Config c)
    {
        if (c.I("mode") == 1)
        {
            if (c.T("bounds").Trim().Length != 0)
                return "bounds (giới hạn riêng) chỉ dùng với mode = 2; chế độ 1 dùng --min/--max.";
            if (c.I("min") > c.I("max")) return "Giá trị nhỏ nhất (min) phải <= giá trị lớn nhất (max).";
            return null;
        }
        if (ParseBounds(c.T("bounds"), out var b) is { } e) return e;
        if (b.Count != c.I("n"))
            return $"Số vị trí trong bounds ({b.Count}) khác n ({Util.LL(c.I("n"))}).";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        long n = c.I("n");
        var b = new List<(long Lo, long Hi)>();
        if (c.I("mode") == 2) ParseBounds(c.T("bounds"), out b);
        char sep = c.I("layout") == 1 ? ' ' : '\n';
        var sb = new StringBuilder((int)Math.Min(int.MaxValue / 2, n * 12 + 16));
        if (c.B("header"))
        {
            Util.AppendLL(sb, n);
            sb.Append('\n');
        }
        for (long i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(sep);
            if (c.I("mode") == 1) Util.AppendLL(sb, rng.Range(c.I("min"), c.I("max")));
            else Util.AppendLL(sb, rng.Range(b[(int)i].Lo, b[(int)i].Hi));
        }
        sb.Append('\n');
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var L) is { } e) return e;
        long n = c.I("n");
        int idx = 0;
        if (c.B("header"))
        {
            if (L.Count == 0 || L[0].Length != 1) return "Dòng 1 phải chứa n.";
            if (ReadIntToken(L[0][0], n, n, "n", out _) is { } e1) return e1;
            idx = 1;
        }
        var toks = new List<string>();
        if (c.I("layout") == 1)
        {
            if (L.Count != idx + 1) return "Các số phải nằm trên đúng một dòng.";
            toks.AddRange(L[idx]);
        }
        else
        {
            if (L.Count - idx != n) return "Phải có đúng n dòng, mỗi dòng một số.";
            for (int k = idx; k < L.Count; k++)
            {
                if (L[k].Length != 1) return $"Dòng {k + 1} phải chứa đúng một số.";
                toks.Add(L[k][0]);
            }
        }
        if (toks.Count != n) return $"Có {toks.Count} số, cần {Util.LL(n)}.";
        var b = new List<(long Lo, long Hi)>();
        if (c.I("mode") == 2) ParseBounds(c.T("bounds"), out b);
        for (int k = 0; k < toks.Count; k++)
        {
            long lo = c.I("mode") == 1 ? c.I("min") : b[k].Lo;
            long hi = c.I("mode") == 1 ? c.I("max") : b[k].Hi;
            if (ReadIntToken(toks[k], lo, hi, $"Số thứ {k + 1}", out _) is { } e2) return e2;
        }
        return null;
    }

    public override ulong EstimateMaxBytes(Config c)
    {
        ulong d = 20;
        if (c.I("mode") == 1) d = Util.MaxDigits(c.I("min"), c.I("max"));
        return (ulong)c.I("n") * (d + 1) + 10;
    }

    public override string DescribeConfig(Config c)
    {
        var s = new StringBuilder();
        s.Append("  - Số lượng số nguyên mỗi test (n): ").Append(Util.LL(c.I("n"))).Append('\n');
        if (c.I("mode") == 1)
        {
            s.Append("  - Chế độ: giới hạn chung [").Append(Util.LL(c.I("min"))).Append(", ")
             .Append(Util.LL(c.I("max"))).Append("]\n");
        }
        else
        {
            s.Append("  - Chế độ: giới hạn riêng từng vị trí\n");
            ParseBounds(c.T("bounds"), out var b);
            int show = Math.Min(b.Count, 20);
            for (int i = 0; i < show; i++)
                s.Append("      Số thứ ").Append(i + 1).Append(": [").Append(Util.LL(b[i].Lo)).Append(", ")
                 .Append(Util.LL(b[i].Hi)).Append("]\n");
            if (b.Count > show) s.Append("      ... (").Append(b.Count - show).Append(" vị trí nữa)\n");
        }
        s.Append("  - Ghi n ở dòng đầu: ").Append(c.B("header") ? "có" : "không").Append('\n');
        s.Append("  - Bố cục: ").Append(c.I("layout") == 1 ? "cùng một dòng" : "mỗi số một dòng").Append('\n');
        return s.ToString();
    }
}
