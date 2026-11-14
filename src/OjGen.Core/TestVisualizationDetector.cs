namespace OjGen.Core;

/// <summary>Heuristic classification of input for the visualization window.</summary>
public static class TestVisualizationDetector
{
    public static string Detect(string[][] tokens)
    {
        if (tokens.Length == 0) return "Phân phối số";
        if (tokens[0].Length >= 2 && int.TryParse(tokens[0][0], out int a) && int.TryParse(tokens[0][1], out int b) && a is > 0 and <= 500 && b is >= 0 and <= 10000)
        {
            if (a <= 200 && b is > 0 and <= 200 && tokens.Length == a + 1 && tokens.Skip(1).All(row => row.Length == 1 && row[0].Length == b)) return "Lưới";
            if (tokens.Length == b + 1 && tokens.Skip(1).All(row => row.Length >= 2 && row.Take(2).All(token => int.TryParse(token, out int v) && v >= 1 && v <= a))) return "Đồ thị / cây";
            if (a <= 200 && b is > 0 and <= 200 && tokens.Length == a + 1 && tokens.Skip(1).All(row => row.All(token => token.Length == 1) && string.Concat(row).Length == b)) return "Lưới";
        }
        if (tokens[0].Length == 1 && int.TryParse(tokens[0][0], out int treeNodes) && treeNodes is > 1 and <= 1000 &&
            tokens.Length == treeNodes && tokens.Skip(1).All(row => row.Length >= 2 && row.Take(2).All(token => int.TryParse(token, out int v) && v >= 1 && v <= treeNodes)))
            return "Đồ thị / cây";
        return "Phân phối số";
    }
}
