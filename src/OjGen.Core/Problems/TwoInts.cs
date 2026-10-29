// TwoInts.cs - Dạng bài có HAI số nguyên a, b.
//
// Định dạng input:
//   a b
using System.Text;

namespace OjGen.Core;

public sealed class TwoIntsProblem : Problem
{
    public override string Id => "two_ints";
    public override string Title => "Hai số nguyên a, b";
    public override string MainCategory => "Số và bộ giá trị";
    public override string FormatDescription =>
        "Một dòng chứa hai số nguyên a b, cách nhau một dấu cách.\n" +
        "  minA <= a <= maxA, minB <= b <= maxB; nếu order = 1 thì luôn có a <= b.\n" +
        "Ví dụ:\n  15 -7\n";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minA", "Giá trị nhỏ nhất của a", 1, long.MinValue, long.MaxValue),
        ParamSpec.Int("maxA", "Giá trị lớn nhất của a", 1000000000, long.MinValue, long.MaxValue),
        ParamSpec.Int("minB", "Giá trị nhỏ nhất của b", 1, long.MinValue, long.MaxValue),
        ParamSpec.Int("maxB", "Giá trị lớn nhất của b", 1000000000, long.MinValue, long.MaxValue),
        ParamSpec.Bool("order", "Bắt buộc a <= b?", false),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minA") > c.I("maxA")) return "minA phải <= maxA.";
        if (c.I("minB") > c.I("maxB")) return "minB phải <= maxB.";
        if (c.B("order") && c.I("minA") > c.I("maxB")) return "Không thể có a <= b vì minA > maxB.";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        long a, b;
        if (c.B("order"))
        {
            a = rng.Range(c.I("minA"), Math.Min(c.I("maxA"), c.I("maxB")));
            b = rng.Range(Math.Max(c.I("minB"), a), c.I("maxB"));
        }
        else
        {
            a = rng.Range(c.I("minA"), c.I("maxA"));
            b = rng.Range(c.I("minB"), c.I("maxB"));
        }
        var sb = new StringBuilder();
        Util.AppendLL(sb, a);
        sb.Append(' ');
        Util.AppendLL(sb, b);
        sb.Append('\n');
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var L) is { } e) return e;
        if (L.Count != 1 || L[0].Length != 2) return "Phải có đúng 1 dòng chứa 2 số nguyên.";
        if (ReadIntToken(L[0][0], c.I("minA"), c.I("maxA"), "a", out long a) is { } e1) return e1;
        if (ReadIntToken(L[0][1], c.I("minB"), c.I("maxB"), "b", out long b) is { } e2) return e2;
        if (c.B("order") && a > b) return "Vi phạm điều kiện a <= b.";
        return null;
    }

    public override ulong EstimateMaxBytes(Config c) =>
        Util.MaxDigits(c.I("minA"), c.I("maxA")) + Util.MaxDigits(c.I("minB"), c.I("maxB")) + 2;
}
