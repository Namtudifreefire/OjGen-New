// SingleN.cs - Dạng bài có MỘT số nguyên n.
//
// Định dạng input:
//   n
using System.Text;

namespace OjGen.Core;

public sealed class SingleNProblem : Problem
{
    public override string Id => "single_n";
    public override string Title => "Một số nguyên n";
    public override string FormatDescription =>
        "Một dòng duy nhất chứa số nguyên n (minVal <= n <= maxVal).\n" +
        "Ví dụ:\n  123456\n";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("minVal", "Giá trị nhỏ nhất của n", 1, long.MinValue, long.MaxValue),
        ParamSpec.Int("maxVal", "Giá trị lớn nhất của n", 1000000000, long.MinValue, long.MaxValue),
    };

    public override string? ValidateConfig(Config c)
    {
        if (c.I("minVal") > c.I("maxVal")) return "minVal phải <= maxVal.";
        return null;
    }

    public override string Generate(Rng rng, Config c)
    {
        var sb = new StringBuilder();
        Util.AppendLL(sb, rng.Range(c.I("minVal"), c.I("maxVal")));
        sb.Append('\n');
        return sb.ToString();
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var L) is { } e) return e;
        if (L.Count != 1 || L[0].Length != 1) return "Phải có đúng 1 dòng chứa 1 số nguyên.";
        return ReadIntToken(L[0][0], c.I("minVal"), c.I("maxVal"), "n", out _);
    }

    public override ulong EstimateMaxBytes(Config c) => Util.MaxDigits(c.I("minVal"), c.I("maxVal")) + 1;
}
