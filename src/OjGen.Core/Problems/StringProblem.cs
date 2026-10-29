// StringProblem.cs - Dạng bài xâu ký tự.
//
// Định dạng input:
//   [len]      (chỉ khi printLen = 1)
//   s
using System.Text;

namespace OjGen.Core;

public sealed class StringProblem : Problem
{
    public override string Id => "string";
    public override string Title => "Xâu ký tự";
    public override string MainCategory => "Xâu và ký tự";
    public override string FormatDescription =>
        "Nếu printLen = 1: dòng 1 là độ dài L của xâu.\n" +
        "Dòng tiếp theo: xâu s gồm L ký tự (minLen <= L <= maxLen) lấy từ tập charset.\n" +
        "charset: lower (a-z), upper (A-Z), letters, digits, alnum, binary (01), dna (ACGT),\n" +
        "  hoặc tự liệt kê ký tự, vd: abc  hoặc  set:xyz# (tiền tố set: để ép hiểu là danh sách).\n" +
        "Ví dụ (printLen = 1, charset = lower):\n  5\n  hello\n";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minLen", "Độ dài nhỏ nhất", 1, 1, 10000000),
        ParamSpec.Int("maxLen", "Độ dài lớn nhất", 20, 1, 10000000),
        ParamSpec.Text("charset", "Tập ký tự", "lower",
            "lower | upper | letters | digits | alnum | binary | dna | hoặc danh sách ký tự, vd abc",
            choices: new[] { "lower", "upper", "letters", "digits", "alnum", "binary", "dna" }),
        ParamSpec.Bool("printLen", "Ghi độ dài xâu ở dòng đầu?", false),
    };

    /// <summary>Phân tích tập ký tự: tên có sẵn hoặc danh sách ký tự cụ thể.</summary>
    internal static string? ResolveCharset(string spec, out string result)
    {
        string k = spec.Trim().ToLowerInvariant();
        var sb = new StringBuilder();
        void RangeAdd(char a, char b) { for (char ch = a; ch <= b; ch++) sb.Append(ch); }
        result = "";
        switch (k)
        {
            case "lower": RangeAdd('a', 'z'); break;
            case "upper": RangeAdd('A', 'Z'); break;
            case "letters": RangeAdd('A', 'Z'); RangeAdd('a', 'z'); break;
            case "digits": RangeAdd('0', '9'); break;
            case "alnum": RangeAdd('0', '9'); RangeAdd('A', 'Z'); RangeAdd('a', 'z'); break;
            case "binary": sb.Append("01"); break;
            case "dna": sb.Append("ACGT"); break;
            default:
            {
                string lit = spec;
                if (lit.StartsWith("set:", StringComparison.Ordinal)) lit = lit[4..];
                var seen = new HashSet<char>();
                foreach (char ch in lit)
                {
                    if (ch < 33 || ch > 126)
                        return "Tập ký tự chỉ được chứa ký tự ASCII in được, không gồm dấu cách/tiếng Việt có dấu.";
                    if (seen.Add(ch)) sb.Append(ch);
                }
                if (sb.Length == 0) return "Tập ký tự rỗng.";
                break;
            }
        }
        result = sb.ToString();
        return null;
    }

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minLen") > c.I("maxLen")) return "minLen phải <= maxLen.";
        return ResolveCharset(c.T("charset"), out _);
    }

    public override string Generate(Rng rng, Config c)
    {
        ResolveCharset(c.T("charset"), out string cs);
        long len = rng.Range(c.I("minLen"), c.I("maxLen"));
        var sb = new StringBuilder((int)len + 24);
        if (c.B("printLen"))
        {
            Util.AppendLL(sb, len);
            sb.Append('\n');
        }
        for (long i = 0; i < len; i++) sb.Append(cs[(int)rng.Below((ulong)cs.Length)]);
        sb.Append('\n');
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var L) is { } e) return e;
        int expectLines = c.B("printLen") ? 2 : 1;
        if (L.Count != expectLines) return $"Số dòng phải là {expectLines}.";
        var sl = L[^1];
        if (sl.Length != 1) return "Xâu không được chứa dấu cách.";
        string s = sl[0];
        long len = s.Length;
        if (len < c.I("minLen") || len > c.I("maxLen")) return "Độ dài xâu nằm ngoài [minLen, maxLen].";
        if (c.B("printLen"))
        {
            if (L[0].Length != 1) return "Dòng 1 phải chứa đúng độ dài xâu.";
            if (ReadIntToken(L[0][0], c.I("minLen"), c.I("maxLen"), "Độ dài", out long l0) is { } e1) return e1;
            if (l0 != len) return "Độ dài ghi ở dòng 1 khác độ dài thực của xâu.";
        }
        ResolveCharset(c.T("charset"), out string cs);
        foreach (char ch in s)
            if (cs.IndexOf(ch) < 0) return $"Ký tự '{ch}' không thuộc charset.";
        return null;
    }

    public override ulong EstimateMaxBytes(Config c) => (ulong)c.I("maxLen") + 12;
}
