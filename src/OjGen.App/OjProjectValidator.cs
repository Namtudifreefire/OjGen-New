using System.IO;
using OjGen.Core;

namespace OjGen.Gui;

/// <summary>Collects restorable project errors before MainWindow mutates the current session.</summary>
internal static class OjProjectValidator
{
    public static IReadOnlyList<string> ValidateForOpen(OjProjectDocument project)
    {
        var errors = new List<string>();
        Problem? problem = Problems.Find(project.ProblemId ?? "");
        if (problem == null)
        {
            errors.Add($"Khuôn bài '{project.ProblemId ?? "(trống)"}' không có trong phiên bản OjGen hiện tại.");
            return errors;
        }

        if (project.TestCount is < 1 or > Generator.MaxTests)
            errors.Add($"Số lượng test phải nằm trong [1, {Generator.MaxTests}].");
        if (!string.IsNullOrWhiteSpace(project.Seed) && !Util.ParseULL(project.Seed, out _))
            errors.Add("Seed phải là số nguyên không âm hoặc để trống.");
        if (project.Resume && project.Overwrite)
            errors.Add("Không thể bật đồng thời Tiếp tục và Ghi đè.");
        string problemCode = project.ProblemCode ?? "";
        if (problemCode.Length > 64 || problemCode.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
            errors.Add("Mã bài chỉ được gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới (tối đa 64 ký tự).");

        Config config = problem.DefaultConfig();
        foreach (var parameter in problem.Params)
        {
            try
            {
                if (parameter.Type == ParamType.Text)
                {
                    if (project.Texts?.TryGetValue(parameter.Key, out string? value) == true)
                        config.SetText(parameter.Key, value ?? "");
                }
                else if (project.Ints?.TryGetValue(parameter.Key, out long value) == true)
                    config.Set(parameter.Key, value);
            }
            catch (Exception ex)
            {
                errors.Add($"Tham số '{parameter.DisplayLabel}' không hợp lệ: {ex.Message}");
            }
        }
        if (Generator.ValidateFullConfig(problem, config) is { } configError)
            errors.Add("Cấu hình chung: " + configError);

        if (project.Statement is { } statement)
        {
            if (statement.Version != 1) errors.Add($"Bản đề dùng phiên bản không được hỗ trợ ({statement.Version}).");
            if (!string.Equals(statement.ProblemId, problem.Id, StringComparison.OrdinalIgnoreCase))
                errors.Add("Bản đề trong dự án đang gắn với một khuôn khác.");
        }

        if (project.TestPlan == null)
            errors.Add("Thiếu kế hoạch nhóm test.");
        else if (project.TestPlan.Groups == null)
            errors.Add("Danh sách nhóm trong kế hoạch bị thiếu.");
        else if (project.TestPlan.Groups.Count > 0)
        {
            if (project.TestPlan.Groups.Any(group => group == null))
                errors.Add("Kế hoạch có nhóm bị rỗng.");
            else
            {
                try { _ = project.TestPlan.ToGroups(problem); }
                catch (Exception ex) { errors.Add("Kế hoạch nhóm test: " + ex.Message); }
            }
        }

        ValidateManualTests(project.ManualTests, problem, "Test nhập tay", errors);
        if (project.LastGeneratedCount < 0)
            errors.Add("Số lượng test của lần sinh gần nhất không hợp lệ.");

        bool canRestoreLastGeneration = project.LastGeneratedCount > 0 &&
            string.Equals(project.LastGeneratedProblemId, project.ProblemId, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(project.LastTestDirectory);
        if (canRestoreLastGeneration)
        {
            Config generatedConfig = problem.DefaultConfig();
            foreach (var parameter in problem.Params)
            {
                try
                {
                    if (parameter.Type == ParamType.Text)
                    {
                        if (project.LastGeneratedTexts?.TryGetValue(parameter.Key, out string? value) == true)
                            generatedConfig.SetText(parameter.Key, value ?? "");
                    }
                    else if (project.LastGeneratedInts?.TryGetValue(parameter.Key, out long value) == true)
                        generatedConfig.Set(parameter.Key, value);
                }
                catch (Exception ex)
                {
                    errors.Add($"Lần sinh gần nhất, tham số '{parameter.DisplayLabel}': {ex.Message}");
                }
            }
            if (Generator.ValidateFullConfig(problem, generatedConfig) is { } generatedConfigError)
                errors.Add("Cấu hình của bộ test đã tạo: " + generatedConfigError);

            List<TestGroupSpec> generatedGroups = new();
            if (project.LastGeneratedPlan == null || project.LastGeneratedPlan.Groups == null)
                errors.Add("Thiếu kế hoạch nhóm của bộ test đã tạo.");
            else if (project.LastGeneratedPlan.Groups.Count > 0)
            {
                if (project.LastGeneratedPlan.Groups.Any(group => group == null))
                    errors.Add("Kế hoạch của bộ test đã tạo có nhóm bị rỗng.");
                else
                {
                    try { generatedGroups = project.LastGeneratedPlan.ToGroups(problem); }
                    catch (Exception ex) { errors.Add("Kế hoạch của bộ test đã tạo: " + ex.Message); }
                }
            }

            ValidateManualTests(project.LastGeneratedManualTests, problem, "Test của bộ đã tạo", errors);
            int generatedCount = project.LastGeneratedCount - (project.LastGeneratedManualTests?.Count ?? 0);
            if (generatedCount < 0 || (generatedGroups.Count > 0 && generatedGroups.Sum(group => group.TestCount) != generatedCount))
                errors.Add("Số test của kế hoạch không khớp bộ test đã tạo.");
            try { _ = FileManager.ListFilesWithExt(project.LastTestDirectory, ".inp"); }
            catch (Exception ex) { errors.Add("Không thể đọc thư mục test gần nhất: " + ex.Message); }
        }

        return errors;
    }

    private static void ValidateManualTests(IReadOnlyList<ProjectManualTest>? savedTests, Problem problem,
        string section, List<string> errors)
    {
        if (savedTests == null) { errors.Add($"{section}: danh sách bị thiếu."); return; }
        for (int index = 0; index < savedTests.Count; index++)
        {
            ProjectManualTest? saved = savedTests[index];
            if (saved == null)
            {
                errors.Add($"{section} #{index + 1}: dữ liệu test bị rỗng.");
                continue;
            }
            string name = string.IsNullOrWhiteSpace(saved.Name) ? $"#{index + 1}" : $"'{saved.Name}'";
            try
            {
                ManualTestSpec manual = saved.ToSpec(problem);
                string? configError = Generator.ValidateFullConfig(problem, manual.Cfg);
                if (configError is not null)
                    errors.Add($"{section} {name}, cấu hình: {configError}");
                else if (problem.ValidateInput(manual.Input, manual.Cfg) is { } inputError)
                    errors.Add($"{section} {name}, input: {inputError}");
            }
            catch (Exception ex)
            {
                errors.Add($"{section} {name}: không đọc được dữ liệu ({ex.Message}).");
            }
        }
    }
}
