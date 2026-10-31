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
               WebUtility.HtmlEncode(Name) + "</title><style>body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:32px auto;padding:0 24px;line-height:1.55;color:#202124}h1,h2{color:#174ea6}pre{background:#f1f3f4;padding:14px;overflow:auto}code{font-family:Consolas,monospace;background:#f1f3f4;padding:2px 4px}.meta{color:#5f6368;border-bottom:1px solid #ddd;padding-bottom:12px}</style></head><body>" + body + "</body></html>";
    }

    private static string MarkdownToHtml(string markdown)
    {
        var sb = new StringBuilder();
        bool code = false, paragraph = false, list = false;
        var codeLines = new StringBuilder();
        void CloseParagraph() { if (paragraph) { sb.Append("</p>"); paragraph = false; } }
        void CloseList() { if (list) { sb.Append("</ul>"); list = false; } }
        foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
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
                if (!list) { sb.Append("<ul>"); list = true; }
                sb.Append("<li>").Append(InlineMarkdown(line[2..])).Append("</li>");
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

    private static string InlineMarkdown(string text)
    {
        string safe = WebUtility.HtmlEncode(text);
        safe = Regex.Replace(safe, @"`([^`]+)`", "<code>$1</code>");
        safe = Regex.Replace(safe, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        return safe;
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

    public static ProblemAuthoringDocument Load(string path)
    {
        var doc = JsonSerializer.Deserialize<ProblemAuthoringDocument>(File.ReadAllText(path));
        if (doc == null || doc.Version != 1) throw new InvalidDataException("Phiên bản bản nháp không được hỗ trợ.");
        return doc;
    }
}
