using OjGen.Core;

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
        if (document.Input.Trim() != problem.FormatDescription.TrimEnd())
            result.Warnings.Add("Mô tả input đã khác cấu trúc khuôn generator; hãy đối chiếu thủ công.");
        if (markedSamples.Count == 0) result.Warnings.Add("Chưa có test được đánh dấu Sample.");
        if (document.Examples.Trim().StartsWith("<!-- Dùng nút Chèn sample", StringComparison.OrdinalIgnoreCase))
            result.Warnings.Add("Phần Ví dụ vẫn là hướng dẫn mặc định, chưa được thay bằng sample.");
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
            if (sample.Input.Trim().Length > 0 && !document.Examples.Contains(sample.Input.Trim(), StringComparison.Ordinal))
                result.Warnings.Add($"Ví dụ '{sample.Name}' chưa được chèn vào phần Ví dụ của đề.");
            string exampleOutput = sample.ExpectedOutput.Length > 0 ? sample.ExpectedOutput : sample.GeneratedOutput;
            if (exampleOutput.Trim().Length > 0 && !document.Examples.Contains(exampleOutput.Trim(), StringComparison.Ordinal))
                result.Warnings.Add($"Output của ví dụ '{sample.Name}' chưa có trong phần Ví dụ của đề.");
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
