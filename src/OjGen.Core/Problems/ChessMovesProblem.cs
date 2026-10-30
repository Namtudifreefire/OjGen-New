// ChessMovesProblem.cs - sinh tọa độ quân Mã trên bàn cờ 8x8.
namespace OjGen.Core;

public sealed class ChessMovesProblem : Problem
{
    private const int BoardSize = 8;

    private static readonly (int Row, int Col)[] Corners =
    {
        (0, 0), (0, BoardSize - 1), (BoardSize - 1, 0), (BoardSize - 1, BoardSize - 1),
    };

    private static readonly (int Row, int Col)[] Edges =
        Enumerable.Range(1, BoardSize - 2)
            .SelectMany(i => new[]
            {
                (0, i), (BoardSize - 1, i), (i, 0), (i, BoardSize - 1),
            })
            .ToArray();

    public override string Id => "chess_moves";
    public override string Title => "Cờ vua: số nước đi của quân Mã";
    public override string MainCategory => "Dữ liệu tùy chỉnh";

    public override string FormatDescription =>
        "Bàn cờ 8×8, tọa độ (x,y) đánh số từ (0,0) đến (7,7). Quân Mã đi theo luật chuẩn: đi 2 ô theo một chiều và 1 ô theo chiều còn lại.\n" +
        "Input gồm một dòng chứa x y. Output do lời giải chuẩn tạo gồm một số duy nhất: số vị trí hợp lệ Mã có thể đi tới.\n" +
        "Ví dụ: `0 0` cho output `2`.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ParamSpec.Int("position", "Vị trí (0 = bất kỳ)", 0, 0, 3,
            options: new[]
            {
                new ParamOption(0, "Bất kỳ"),
                new ParamOption(1, "Góc bàn cờ"),
                new ParamOption(2, "Cạnh bàn cờ (không ở góc)"),
                new ParamOption(3, "Vùng trung tâm (hàng/cột 2–5)"),
            }),
    };

    public override string? ValidateConfig(Config c) => null;

    public override string Generate(Rng rng, Config c)
    {
        var (x, y) = PickPosition(rng, (int)c.I("position"));
        return $"{x} {y}\n";
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (SplitInputLines(input, out var lines) is { } error) return error;
        if (lines.Count != 1 || lines[0].Length != 2)
            return "Input phải có đúng một dòng gồm hai số nguyên x y.";

        if (ReadIntToken(lines[0][0], 0, BoardSize - 1, "x", out long x) is { } xError) return xError;
        if (ReadIntToken(lines[0][1], 0, BoardSize - 1, "y", out long y) is { } yError) return yError;

        bool onEdge = x == 0 || x == BoardSize - 1 || y == 0 || y == BoardSize - 1;
        bool corner = (x == 0 || x == BoardSize - 1) && (y == 0 || y == BoardSize - 1);
        if (c.I("position") == 1 && !corner) return "Vị trí phải ở một góc bàn cờ.";
        if (c.I("position") == 2 && (!onEdge || corner)) return "Vị trí phải ở cạnh bàn cờ, không phải góc.";
        if (c.I("position") == 3 && (x < 2 || x > 5 || y < 2 || y > 5))
            return "Vị trí phải ở vùng trung tâm bàn cờ (x và y từ 2 đến 5).";

        return null;
    }

    public override ulong EstimateMaxBytes(Config c) => 16;

    public override string DescribeConfig(Config c)
    {
        string position = c.I("position") switch
        {
            1 => "góc bàn cờ",
            2 => "cạnh bàn cờ, không ở góc",
            3 => "vùng trung tâm bàn cờ",
            _ => "bất kỳ",
        };
        return $"  - Bàn cờ: {BoardSize}×{BoardSize}, tọa độ 0..{BoardSize - 1}\n" +
               $"  - Vị trí: {position}\n";
    }

    private static (int Row, int Col) PickPosition(Rng rng, int mode) => mode switch
    {
        1 => Corners[(int)rng.Below((ulong)Corners.Length)],
        2 => Edges[(int)rng.Below((ulong)Edges.Length)],
        3 => ((int)rng.Range(2, 5), (int)rng.Range(2, 5)),
        _ => ((int)rng.Range(0, BoardSize - 1), (int)rng.Range(0, BoardSize - 1)),
    };

}
