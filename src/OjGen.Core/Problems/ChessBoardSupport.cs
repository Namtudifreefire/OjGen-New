namespace OjGen.Core;

/// <summary>Chia sẻ quy tắc bàn cờ 8×8 cho các khuôn bài cờ vua.</summary>
internal static class ChessBoardSupport
{
    public const int Size = 8;

    private static readonly (int X, int Y)[] Corners =
    {
        (0, 0), (0, 7), (7, 0), (7, 7),
    };

    private static readonly (int X, int Y)[] Edges = Enumerable.Range(1, Size - 2)
        .SelectMany(i => new[] { (0, i), (7, i), (i, 0), (i, 7) })
        .ToArray();

    public static ParamSpec PositionParam() => ParamSpec.Int(
        "position", "Vùng chọn tọa độ", 0, 0, 3,
        options: new[]
        {
            new ParamOption(0, "Bất kỳ"),
            new ParamOption(1, "Góc bàn cờ"),
            new ParamOption(2, "Cạnh bàn cờ (không ở góc)"),
            new ParamOption(3, "Vùng trung tâm (2–5)"),
        });

    public static (int X, int Y) PickPosition(Rng rng, long mode) => mode switch
    {
        1 => Corners[(int)rng.Below((ulong)Corners.Length)],
        2 => Edges[(int)rng.Below((ulong)Edges.Length)],
        3 => ((int)rng.Range(2, 5), (int)rng.Range(2, 5)),
        _ => ((int)rng.Range(0, Size - 1), (int)rng.Range(0, Size - 1)),
    };

    public static string? ParseCoordinates(string input, int count, out int[] coordinates)
    {
        coordinates = Array.Empty<int>();
        if (Problem.SplitInputLines(input, out var lines) is { } error) return error;
        if (lines.Count != 1 || lines[0].Length != count)
            return $"Input phải có đúng một dòng gồm {count} số nguyên tọa độ.";

        coordinates = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (Problem.ReadIntToken(lines[0][i], 0, Size - 1, $"Tọa độ thứ {i + 1}", out long value) is { } tokenError)
                return tokenError;
            coordinates[i] = (int)value;
        }
        return null;
    }

    public static string? ValidatePositionMode(int x, int y, long mode)
    {
        bool onEdge = x == 0 || x == Size - 1 || y == 0 || y == Size - 1;
        bool corner = (x == 0 || x == Size - 1) && (y == 0 || y == Size - 1);
        if (mode == 1 && !corner) return "Vị trí phải ở một góc bàn cờ.";
        if (mode == 2 && (!onEdge || corner)) return "Vị trí phải ở cạnh bàn cờ, không phải góc.";
        if (mode == 3 && (x < 2 || x > 5 || y < 2 || y > 5))
            return "Vị trí phải ở vùng trung tâm bàn cờ (x và y từ 2 đến 5).";
        return null;
    }

    public static List<(int X, int Y)> KnightTargets(int x, int y) => AllSquares()
        .Where(p => IsKnightMove(x, y, p.X, p.Y))
        .OrderBy(p => p.X)
        .ThenBy(p => p.Y)
        .ToList();

    public static IEnumerable<(int X, int Y)> AllSquares()
    {
        for (int x = 0; x < Size; x++)
        for (int y = 0; y < Size; y++)
            yield return (x, y);
    }

    public static bool IsKnightMove(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x1 - x2), dy = Math.Abs(y1 - y2);
        return dx * dy == 2;
    }

    public static int CountPieceMoves(int piece, int x, int y)
    {
        int count = 0;
        foreach (var (nx, ny) in AllSquares())
        {
            int dx = Math.Abs(nx - x), dy = Math.Abs(ny - y);
            bool canMove = piece switch
            {
                1 => dx * dy == 2,                         // Mã
                2 => Math.Max(dx, dy) == 1,               // Vua
                3 => (x == nx) != (y == ny),              // Xe
                4 => dx == dy && dx > 0,                  // Tượng
                5 => (x == nx || y == ny || dx == dy) && (dx > 0 || dy > 0), // Hậu
                _ => false,
            };
            if (canMove) count++;
        }
        return count;
    }
}
