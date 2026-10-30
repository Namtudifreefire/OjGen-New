namespace OjGen.Core;

/// <summary>Liệt kê mọi ô quân Mã có thể đi tới từ một tọa độ.</summary>
public sealed class ChessKnightTargetsProblem : Problem
{
    public override string Id => "chess_knight_targets";
    public override string Title => "Cờ vua: liệt kê ô quân Mã có thể đi";
    public override string MainCategory => "Dữ liệu tùy chỉnh";
    public override string FormatDescription =>
        "Bàn cờ trống 8×8, tọa độ (x,y) từ (0,0) đến (7,7).\n" +
        "Input gồm một dòng x y là vị trí quân Mã.\n" +
        "Output: dòng đầu là k, số ô đi được; k dòng sau mỗi dòng ghi x y của một ô đích. " +
        "Liệt kê theo thứ tự tăng dần của x, rồi y.";

    public override IReadOnlyList<ParamSpec> Params { get; } = new[] { ChessBoardSupport.PositionParam() };

    public override string? ValidateConfig(Config c) => null;

    public override string Generate(Rng rng, Config c)
    {
        var (x, y) = ChessBoardSupport.PickPosition(rng, c.I("position"));
        return $"{x} {y}\n";
    }

    public override string? ValidateInput(string input, Config c)
    {
        if (ChessBoardSupport.ParseCoordinates(input, 2, out var v) is { } error) return error;
        return ChessBoardSupport.ValidatePositionMode(v[0], v[1], c.I("position"));
    }

    public override ulong EstimateMaxBytes(Config c) => 16;

    public override string DescribeConfig(Config c) =>
        $"  - Bàn cờ: {ChessBoardSupport.Size}×{ChessBoardSupport.Size}, tọa độ 0..{ChessBoardSupport.Size - 1}\n" +
        $"  - Vùng tọa độ: {c.I("position") switch { 1 => "góc", 2 => "cạnh, không ở góc", 3 => "trung tâm", _ => "bất kỳ" }}\n";
}
