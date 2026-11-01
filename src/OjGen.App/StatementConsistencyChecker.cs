using OjGen.Core;
using System.Text.RegularExpressions;

namespace OjGen.Gui;

internal sealed class StatementCheckResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public string GeneratorConfiguration { get; set; } = "";
}

internal static class StatementConsistencyChecker
{
    private static string NormalizeOutput(string text) => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string NormalizeInputForComparison(string text) => ManualTestSpec.NormalizeInput(text).TrimEnd('\n');

    private sealed record ExamplePair(string Input, string Output);

    private static List<ExamplePair> ParseExamplePairs(string examples)
    {
        const string fence = "```";
        string pattern = $@"(?:^|\r?\n)[ \t]*(?:#{{1,6}}[ \t]+[^\r\n]*\r?\n[ \t]*)?(?:Input|Dữ liệu vào)[ \t]*:?\s*\r?\n[ \t]*{Regex.Escape(fence)}[^\r\n]*\r?\n(?<input>.*?)\r?\n[ \t]*{Regex.Escape(fence)}\s*(?:Output|Dữ liệu ra)[ \t]*:?\s*\r?\n[ \t]*{Regex.Escape(fence)}[^\r\n]*\r?\n(?<output>.*?)\r?\n[ \t]*{Regex.Escape(fence)}";
        return Regex.Matches(examples, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .Select(match => new ExamplePair(
                NormalizeInputForComparison(match.Groups["input"].Value),
                match.Groups["output"].Value.TrimEnd('\r', '\n')))
            .ToList();
    }

    public static StatementCheckResult Check(ProblemAuthoringDocument document, Problem problem, Config config,
                                             IReadOnlyList<ManualTestSpec> markedSamples)
    {
        var result = new StatementCheckResult { GeneratorConfiguration = problem.DescribeConfig(config) };
        if (document.ProblemId != problem.Id) result.Errors.Add("Bản đề đang gắn với khuôn input khác.");
        if (document.Name.Trim().Length == 0) result.Errors.Add("Tên bài đang để trống.");
        if (document.Code.Trim().Length == 0) result.Errors.Add("Mã bài đang để trống.");
        if (document.Code.Length > 64 || document.Code.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
            result.Errors.Add("Mã bài chỉ được gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới (tối đa 64 ký tự).");
        if (document.TimeLimitMs < 1) result.Errors.Add("Thời gian phải lớn hơn 0 ms.");
        if (document.MemoryLimitMb < 1) result.Errors.Add("Bộ nhớ phải lớn hơn 0 MB.");
        if (!document.LimitsConfirmed)
            result.Warnings.Add("Chưa xác nhận giới hạn thời gian và bộ nhớ từ đề gốc; hãy đối chiếu trước khi phát hành.");
        if (document.Description.Trim().Length == 0) result.Errors.Add("Chưa viết phần đề bài.");
        else if (document.Description.Trim().Equals("Mô tả ý tưởng và yêu cầu của bài toán tại đây.", StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Phần đề bài vẫn là nội dung gợi ý mặc định.");
        if (document.Input.Trim().Length == 0) result.Errors.Add("Chưa mô tả input.");
        if (document.Output.Trim().Length == 0) result.Errors.Add("Chưa mô tả output.");
        else if (document.Output.Trim().Equals("Mô tả định dạng output tại đây.", StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Phần output vẫn là nội dung gợi ý mặc định.");
        if (document.Constraints.Trim().Length == 0) result.Errors.Add("Chưa ghi giới hạn.");
        else if (document.Constraints.Trim().Equals("Điền các giới hạn chính thức của đề bài tại đây.", StringComparison.OrdinalIgnoreCase))
            result.Errors.Add("Phần giới hạn vẫn là nội dung gợi ý mặc định.");
        if (document.Description.Trim().Length is > 0 and < 30)
            result.Warnings.Add("Mô tả đề khá ngắn; hãy rà lại mục tiêu, yêu cầu và trường hợp đặc biệt.");
        if (!document.Constraints.Any(char.IsDigit))
            result.Warnings.Add("Không thấy số trong phần giới hạn; kiểm tra đã ghi đủ cận dữ liệu.");
        string statementText = string.Join("\n", document.Description, document.Input, document.Output, document.Constraints, document.Examples);
        if (!Regex.IsMatch(statementText, @"\bO\s*\(", RegexOptions.IgnoreCase))
            result.Warnings.Add("Chưa thấy độ phức tạp dạng O(...); cân nhắc ghi để người giải hiểu giới hạn thời gian.");
        List<ExamplePair> examplePairs = ParseExamplePairs(document.Examples);
        if (examplePairs.Count == 0 && markedSamples.Count > 0)
            result.Warnings.Add("Chưa nhận diện được cặp Input/Output dạng code fence trong Ví dụ; dùng nút Chèn/cập nhật từ test Sample để tạo đúng định dạng.");
        if (document.Input.Trim() != problem.FormatDescription.TrimEnd())
            result.Warnings.Add("Mô tả input đã khác cấu trúc khuôn generator; hãy đối chiếu thủ công.");
        if (markedSamples.Count == 0) result.Warnings.Add("Chưa có test được đánh dấu Sample.");
        if (document.Examples.Trim().StartsWith("<!-- Dùng nút Chèn sample", StringComparison.OrdinalIgnoreCase))
            result.Warnings.Add("Phần Ví dụ vẫn là hướng dẫn mặc định, chưa được thay bằng sample.");
        foreach (var example in examplePairs)
        {
            if (!markedSamples.Any(sample => NormalizeInputForComparison(sample.Input) == example.Input))
                result.Warnings.Add("Phần Ví dụ có input không còn tương ứng với test nào đang được đánh dấu Sample.");
        }
        foreach (var sample in markedSamples)
        {
            if (problem.ValidateInput(sample.Input, sample.Cfg) is { } inputError)
                result.Errors.Add($"Sample '{sample.Name}' không hợp lệ: {inputError}");
            if (sample.ExpectedOutput.Trim().Length == 0 && sample.GeneratedOutput.Trim().Length == 0)
                result.Warnings.Add($"Sample '{sample.Name}' chưa có output mong đợi.");
            else if (sample.GeneratedOutput.Trim().Length == 0)
                result.Warnings.Add($"Output sample '{sample.Name}' chưa được đối chiếu với lời giải chuẩn.");
            else if (sample.ExpectedOutput.Trim().Length > 0 && sample.GeneratedOutput.Trim().Length > 0 &&
                     NormalizeOutput(sample.ExpectedOutput) != NormalizeOutput(sample.GeneratedOutput))
                result.Errors.Add($"Output sample '{sample.Name}' khác output của lời giải chuẩn.");
            string normalizedInput = NormalizeInputForComparison(sample.Input);
            List<ExamplePair> matchingInputs = examplePairs.Where(pair => pair.Input == normalizedInput).ToList();
            string referenceOutput = sample.GeneratedOutput.Trim().Length > 0 ? sample.GeneratedOutput : sample.ExpectedOutput;
            if (matchingInputs.Count == 0)
                result.Warnings.Add($"Cặp Input/Output trong Ví dụ chưa khớp chính xác với test Sample '{sample.Name}'; hãy chèn/cập nhật lại sample.");
            else if (referenceOutput.Trim().Length > 0 && !matchingInputs.Any(pair => NormalizeOutput(pair.Output) == NormalizeOutput(referenceOutput)))
                result.Errors.Add($"Output trong Ví dụ của sample '{sample.Name}' khác output của lời giải chuẩn/test đã lưu.");
        }
        return result;
    }

    public static string Format(StatementCheckResult check)
    {
        var lines = new List<string>();
        if (check.Errors.Count == 0) lines.Add("Kiểm tra cấu trúc: đạt.");
        else
        {
            lines.Add("Lỗi cần sửa:");
            lines.AddRange(check.Errors.Select(error => "• " + error));
        }
        if (check.Warnings.Count > 0)
        {
            lines.Add("");
            lines.Add("Cần rà lại:");
            lines.AddRange(check.Warnings.Select(warning => "• " + warning));
        }
        lines.Add("");
        lines.Add("Đối chiếu cấu hình generator:");
        lines.Add(check.GeneratorConfiguration);
        return string.Join(Environment.NewLine, lines);
    }
}
