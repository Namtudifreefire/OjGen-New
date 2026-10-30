namespace OjGen.Core;

/// <summary>Tìm số nước Mã ít nhất giữa hai ô trên bàn cờ trống 8×8.</summary>
public sealed class ChessKnightShortestPathProblem : Problem
{
    public override string Id => "chess_knight_shortest";
    public override string Title => "Cờ vua: số nước Mã ít nhất giữa hai ô";
    public override string MainCategory => "Dữ liệu tùy chỉnh";
    public override string FormatDescription =>
        "Bàn cờ trống 8×8, tọa độ (x,y) từ (0,0) đến (7,7), không có quân cờ cản đường.\n" +
        "Input gồm một dòng x1 y1 x2 y2, lần lượt là ô bắt đầu và ô đích của quân Mã.\n" +
        "Output là số nước đi ít nhất để Mã tới ô đích.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[]
    {
        ChessBoardSupport.PositionParam(),
        ParamSpec.Int("targetMode", "Loại ô đích", 0, 0, 3,
            options: new[]
            {
                new ParamOption(0, "Ngẫu nhiên"),
                new ParamOption(1, "Trùng ô bắt đầu (0 nước)"),
                new ParamOption(2, "Cách một nước Mã"),
                new ParamOption(3, "Ô đối diện qua tâm bàn cờ"),
            }),
    };

    public override string? ValidateConfig(Config c) => null;

    public override string Generate(Rng rng, Config c)
    {
        var (x1, y1) = ChessBoardSupport.PickPosition(rng, c.I("position"));
        var oneMoveTargets = c.I("targetMode") == 2 ? ChessBoardSupport.KnightTargets(x1, y1) : null;
        (int X, int Y) target = c.I("targetMode") switch
        {
            1 => (x1, y1),
            2 => oneMoveTargets![(int)rng.Below((ulong)oneMoveTargets!.Count)],
            3 => (ChessBoardSupport.Size - 1 - x1, ChessBoardSupport.Size - 1 - y1),
            _ => ChessBoardSupport.AllSquares().ElementAt((int)rng.Below((ulong)(ChessBoardSupport.Size * ChessBoardSupport.Size))),
        };
        return $"{x1} {y1} {target.X} {target.Y}\n";
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (ChessBoardSupport.ParseCoordinates(input, 4, out var v) is { } error) return error;
        if (ChessBoardSupport.ValidatePositionMode(v[0], v[1], c.I("position")) is { } positionError) return positionError;
        if (c.I("targetMode") == 1 && (v[0] != v[2] || v[1] != v[3])) return "Ô đích phải trùng ô bắt đầu.";
        if (c.I("targetMode") == 2 && !ChessBoardSupport.IsKnightMove(v[0], v[1], v[2], v[3]))
            return "Ô đích phải cách ô bắt đầu đúng một nước Mã.";
        if (c.I("targetMode") == 3 && (v[2] != ChessBoardSupport.Size - 1 - v[0] || v[3] != ChessBoardSupport.Size - 1 - v[1]))
            return "Ô đích phải đối diện ô bắt đầu qua tâm bàn cờ.";
        return null;
    }

    public override ulong EstimateMaxBytes(Config c) => 32;

    public override string DescribeConfig(Config c) =>
        $"  - Vùng ô bắt đầu: {c.I("position") switch { 1 => "góc", 2 => "cạnh, không ở góc", 3 => "trung tâm", _ => "bất kỳ" }}\n" +
        $"  - Loại ô đích: {c.I("targetMode") switch { 1 => "cùng ô", 2 => "cách một nước Mã", 3 => "đối diện qua tâm", _ => "ngẫu nhiên" }}\n";
}
