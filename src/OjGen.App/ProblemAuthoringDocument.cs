using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OjGen.Core;

namespace OjGen.Gui;

internal sealed class ProblemAuthoringDocument
{
    public int Version { get; set; } = 1;
    public string ProblemId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int TimeLimitMs { get; set; } = 2000;
    public int MemoryLimitMb { get; set; } = 256;
    public string Description { get; set; } = "";
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
    public string Constraints { get; set; } = "";
    public string Examples { get; set; } = "";

    public static ProblemAuthoringDocument Create(Problem p, Config cfg, string name, string code) => new()
    {
        ProblemId = p.Id,
        Name = name.Length == 0 ? p.Title : name,
        Code = code.Length == 0 ? p.Id : code,
        Description = "Mô tả ý tưởng và yêu cầu của bài toán tại đây.",
        Input = p.FormatDescription.TrimEnd(),
        Output = "Mô tả định dạng output tại đây.",
        Constraints = "Điền các giới hạn chính thức của đề bài tại đây.",
        Examples = "<!-- Dùng nút Chèn sample để lấy input/output từ các test được đánh dấu Sample. -->",
    };

    public ProblemAuthoringDocument Clone() => (ProblemAuthoringDocument)MemberwiseClone();

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(Name).Append("\n\n");
        if (Code.Length > 0) sb.Append("Mã bài: `").Append(Code).Append("`\n\n");
        sb.Append("## Đề bài\n\n").Append(Description.Trim()).Append("\n\n");
        sb.Append("## Input\n\n").Append(Input.Trim()).Append("\n\n");
        sb.Append("## Output\n\n").Append(Output.Trim()).Append("\n\n");
        sb.Append("## Giới hạn\n\n").Append(Constraints.Trim()).Append("\n\n");
        sb.Append("Thời gian: ").Append(TimeLimitMs).Append(" ms · Bộ nhớ: ").Append(MemoryLimitMb).Append(" MB\n\n");
        sb.Append("## Ví dụ\n\n").Append(Examples.Trim()).Append('\n');
        return sb.ToString();
    }

    public string ToHtml()
    {
        string body = MarkdownToHtml(ToMarkdown());
        return "<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><title>" +
               WebUtility.HtmlEncode(Name) + "</title><style>body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:32px auto;padding:0 24px;line-height:1.55;color:#202124}h1,h2{color:#174ea6}pre{background:#f1f3f4;padding:14px;overflow:auto}code{font-family:Consolas,monospace;background:#f1f3f4;padding:2px 4px}.math{font-family:Cambria Math,STIX Two Math,serif;font-style:italic;white-space:nowrap}.frac{display:inline-flex;vertical-align:middle;flex-direction:column;text-align:center;line-height:1.1}.frac>span:first-child{border-bottom:1px solid;padding:0 3px}.root{border-top:1px solid;padding:0 3px}table{border-collapse:collapse;margin:12px 0}th,td{border:1px solid #aab;padding:6px 10px}blockquote{border-left:4px solid #8ab4f8;margin-left:0;padding-left:12px;color:#5f6368}a{color:#174ea6}img{max-width:100%;height:auto}</style></head><body>" + body + "</body></html>";
    }

    private static string MarkdownToHtml(string markdown)
    {
        var sb = new StringBuilder();
        bool code = false, paragraph = false, list = false, orderedList = false;
        var codeLines = new StringBuilder();
        void CloseParagraph() { if (paragraph) { sb.Append("</p>"); paragraph = false; } }
        void CloseList() { if (list) { sb.Append(orderedList ? "</ol>" : "</ul>"); list = false; orderedList = false; } }
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string raw = lines[index];
            string line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```") )
            {
                CloseParagraph(); CloseList();
                if (code) { sb.Append("<pre><code>").Append(WebUtility.HtmlEncode(codeLines.ToString())).Append("</code></pre>"); codeLines.Clear(); }
                code = !code;
                continue;
            }
            if (code) { codeLines.AppendLine(line); continue; }
            if (line.Length == 0) { CloseParagraph(); CloseList(); continue; }
            if (line.Contains('|') && index + 1 < lines.Length && Regex.IsMatch(lines[index + 1].Trim(), @"^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$"))
            {
                CloseParagraph(); CloseList();
                var headers = TableCells(line);
                index++;
                sb.Append("<table><thead><tr>");
                foreach (string cell in headers) sb.Append("<th>").Append(InlineMarkdown(cell)).Append("</th>");
                sb.Append("</tr></thead><tbody>");
                for (index++; index < lines.Length && lines[index].Contains('|'); index++)
                {
                    sb.Append("<tr>");
                    foreach (string cell in TableCells(lines[index])) sb.Append("<td>").Append(InlineMarkdown(cell)).Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table>");
                index--;
                continue;
            }
            int hashes = 0;
            while (hashes < line.Length && hashes < 6 && line[hashes] == '#') hashes++;
            if (hashes > 0 && hashes < line.Length && line[hashes] == ' ')
            {
                CloseParagraph(); CloseList();
                sb.Append("<h").Append(hashes).Append('>').Append(InlineMarkdown(line[(hashes + 1)..])).Append("</h").Append(hashes).Append('>');
                continue;
            }
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                CloseParagraph();
                if (list && orderedList) CloseList();
                if (!list) { sb.Append("<ul>"); list = true; }
                sb.Append("<li>").Append(InlineMarkdown(line[2..])).Append("</li>");
                continue;
            }
            if (Regex.Match(line, @"^\d+[.)]\s+(.+)$") is { Success: true } ordered)
            {
                CloseParagraph();
                if (list && !orderedList) CloseList();
                if (!list) { sb.Append("<ol>"); list = true; orderedList = true; }
                sb.Append("<li>").Append(InlineMarkdown(ordered.Groups[1].Value)).Append("</li>");
                continue;
            }
            if (line.StartsWith("> "))
            {
                CloseParagraph(); CloseList();
                sb.Append("<blockquote>").Append(InlineMarkdown(line[2..])).Append("</blockquote>");
                continue;
            }
            CloseList();
            if (!paragraph) { sb.Append("<p>"); paragraph = true; }
            else sb.Append(' ');
            sb.Append(InlineMarkdown(line));
        }
        if (code) sb.Append("<pre><code>").Append(WebUtility.HtmlEncode(codeLines.ToString())).Append("</code></pre>");
        CloseParagraph(); CloseList();
        return sb.ToString();
    }

    private static string[] TableCells(string row) => row.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    private static string InlineMarkdown(string text)
    {
        string safe = WebUtility.HtmlEncode(text);
        safe = Regex.Replace(safe, @"`([^`]+)`", "<code>$1</code>");
        safe = Regex.Replace(safe, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        safe = Regex.Replace(safe, @"(?<!\*)\*([^*]+)\*(?!\*)", "<em>$1</em>");
        safe = Regex.Replace(safe, @"\$([^$]+)\$", match => FormatMath(match.Groups[1].Value));
        safe = Regex.Replace(safe, @"!\[([^\]]*)\]\(([^)\s]+)\)", match =>
        {
            string alt = match.Groups[1].Value, url = match.Groups[2].Value;
            return Uri.TryCreate(WebUtility.HtmlDecode(url), UriKind.Absolute, out var imageUri) && imageUri.Scheme == "https"
                ? $"<img src=\"{url}\" alt=\"{alt}\" loading=\"lazy\" referrerpolicy=\"no-referrer\">"
                : alt;
        });
        safe = Regex.Replace(safe, @"\[([^\]]+)\]\(([^)]+)\)", match =>
        {
            string label = match.Groups[1].Value, url = match.Groups[2].Value;
            bool allowed = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto";
            allowed |= url.StartsWith('#') || url.StartsWith('/') || (!url.Contains(':') && !url.StartsWith("\\\\"));
            return allowed ? $"<a href=\"{url}\">{label}</a>" : label;
        });
        return safe;
    }

    private static string FormatMath(string source)
    {
        string expression = WebUtility.HtmlEncode(source);
        expression = Regex.Replace(expression, @"\\frac\{([^{}]+)\}\{([^{}]+)\}", "<span class=\"frac\"><span>$1</span><span>$2</span></span>");
        expression = Regex.Replace(expression, @"\\sqrt\{([^{}]+)\}", "√<span class=\"root\">$1</span>");
        expression = Regex.Replace(expression, @"(?<=\S)_\{([^{}]+)\}|(?<=\S)_([A-Za-z0-9]+)", match =>
            "<sub>" + (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value) + "</sub>");
        expression = Regex.Replace(expression, @"(?<=\S)\^\{([^{}]+)\}|(?<=\S)\^([A-Za-z0-9+-]+)", match =>
            "<sup>" + (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value) + "</sup>");
        var symbols = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [@"\leq"] = "≤", [@"\le"] = "≤", [@"\geq"] = "≥", [@"\ge"] = "≥", [@"\neq"] = "≠", [@"\ne"] = "≠",
            [@"\times"] = "×", [@"\cdot"] = "·", [@"\div"] = "÷", [@"\pm"] = "±", [@"\infty"] = "∞",
            [@"\in"] = "∈", [@"\notin"] = "∉", [@"\subseteq"] = "⊆", [@"\cup"] = "∪", [@"\cap"] = "∩",
            [@"\to"] = "→", [@"\rightarrow"] = "→", [@"\sum"] = "∑", [@"\prod"] = "∏", [@"\alpha"] = "α",
            [@"\beta"] = "β", [@"\theta"] = "θ", [@"\lambda"] = "λ", [@"\pi"] = "π", [@"\sigma"] = "σ", [@"\omega"] = "ω",
            [@"\left"] = "", [@"\right"] = "",
        };
        foreach (var (latex, glyph) in symbols.OrderByDescending(pair => pair.Key.Length))
            expression = Regex.Replace(expression, Regex.Escape(latex), glyph);
        return "<span class=\"math\">" + expression + "</span>";
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

    public static ProblemAuthoringDocument Load(string path)
    {
        var doc = JsonSerializer.Deserialize<ProblemAuthoringDocument>(File.ReadAllText(path));
        if (doc == null || doc.Version != 1) throw new InvalidDataException("Phiên bản bản nháp không được hỗ trợ.");
        return doc;
    }
}
