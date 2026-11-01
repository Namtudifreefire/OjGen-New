namespace OjGen.Core;

public static class TestCoverageTags
{
    public const int MaxTags = 24;
    public const int MaxLength = 32;

    public static string? Parse(string? text, out List<string> tags)
    {
        tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in (text ?? "").Split(new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string tag = raw.Trim().ToLowerInvariant();
            if (tag.Length == 0) continue;
            if (tag.Length > MaxLength || tag.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-')))
                return $"Nhãn '{tag}' chỉ được gồm chữ ASCII, số, '_' hoặc '-' và dài tối đa {MaxLength} ký tự.";
            if (!seen.Add(tag)) return $"Nhãn độ phủ bị lặp: {tag}.";
            tags.Add(tag);
            if (tags.Count > MaxTags) return $"Chỉ hỗ trợ tối đa {MaxTags} nhãn độ phủ.";
        }
        return null;
    }
}
