using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using OjGen.Core;

namespace OjGen.Gui;

internal sealed class PackageValidationResult
{
    public List<string> Errors { get; } = new();
    public int FileCount { get; set; }
}

/// <summary>Validates the concrete OjGen and ICPC/Kattis packages before they are offered for release.</summary>
internal static class ProblemPackageValidator
{
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool HasFile(string root, string relative) => File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

    public static PackageValidationResult Validate(string root, string format)
    {
        var result = new PackageValidationResult();
        if (!Directory.Exists(root)) { result.Errors.Add("Không tìm thấy thư mục gói."); return result; }
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList();
        result.FileCount = files.Count;
        if (files.Count == 0) { result.Errors.Add("Gói không có file."); return result; }
        if (!HasFile(root, "PACKAGE_SHA256SUMS.txt")) result.Errors.Add("Thiếu PACKAGE_SHA256SUMS.txt.");
        else
        {
            var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(Path.Combine(root, "PACKAGE_SHA256SUMS.txt")))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                int split = line.IndexOf("  ", StringComparison.Ordinal);
                if (split != 64 || line[..split].Any(ch => !Uri.IsHexDigit(ch))) { result.Errors.Add("Dòng checksum sai định dạng."); continue; }
                string relative = line[(split + 2)..].Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar).Contains("..") ||
                    !expected.TryAdd(relative, line[..split])) result.Errors.Add("Tên file trong checksum trùng hoặc không an toàn: " + relative);
            }
            foreach (string file in files.Where(path => !string.Equals(Path.GetFileName(path), "PACKAGE_SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)))
            {
                string relative = Path.GetRelativePath(root, file);
                if (!expected.TryGetValue(relative, out string? hash)) result.Errors.Add("Manifest checksum thiếu " + relative.Replace('\\', '/') + ".");
                else if (!string.Equals(hash, Hash(file), StringComparison.OrdinalIgnoreCase)) result.Errors.Add("SHA-256 không khớp: " + relative.Replace('\\', '/') + ".");
            }
            if (expected.Count != files.Count - 1) result.Errors.Add("Manifest checksum có số mục không khớp với file trong gói.");
        }

        if (format == "Icpc") ValidateIcpc(root, result);
        else ValidateOjGen(root, result);
        return result;
    }

    private static void ValidateOjGen(string root, PackageValidationResult result)
    {
        foreach (string required in new[] { "statement.md", "statement.html", "problem.ojproblem.json", "PACKAGE.txt", "TEST_INVENTORY.csv" })
            if (!HasFile(root, required)) result.Errors.Add("Gói OjGen thiếu " + required + ".");
        if (HasFile(root, "problem.ojproblem.json"))
        {
            try { _ = ProblemProfileFile.Load(Path.Combine(root, "problem.ojproblem.json")); }
            catch (Exception ex) { result.Errors.Add("Hồ sơ problem.ojproblem.json không hợp lệ: " + ex.Message); }
        }
        if (HasFile(root, "TEST_PLAN.ojplan.json"))
        {
            try
            {
                var plan = TestPlanProfile.Load(Path.Combine(root, "TEST_PLAN.ojplan.json"));
                var problem = Problems.Find(plan.ProblemId) ?? throw new InvalidDataException("Không tìm thấy khuôn bài trong gói.");
                _ = plan.ToGroups(problem);
            }
            catch (Exception ex) { result.Errors.Add("Kế hoạch TEST_PLAN.ojplan.json không hợp lệ: " + ex.Message); }
        }
        string tests = Path.Combine(root, "tests");
        if (Directory.Exists(tests))
        {
            var inputs = FileManager.ListFilesWithExt(tests, ".inp");
            if (inputs.Count == 0) result.Errors.Add("Thư mục tests có nhưng không chứa .inp.");
            foreach (string input in inputs)
                if (!File.Exists(Path.ChangeExtension(input, ".out"))) result.Errors.Add("Thiếu output cho " + Path.GetFileName(input) + ".");
            if (FileManager.ListFilesWithExt(tests, ".out").Count != inputs.Count) result.Errors.Add("Số input/output trong tests không bằng nhau.");
        }
        string samples = Path.Combine(root, "samples");
        if (Directory.Exists(samples))
            foreach (string input in FileManager.ListFilesWithExt(samples, ".inp"))
                if (!File.Exists(Path.ChangeExtension(input, ".out"))) result.Errors.Add("Sample thiếu output: " + Path.GetFileName(input) + ".");
    }

    private static void ValidateIcpc(string root, PackageValidationResult result)
    {
        foreach (string required in new[] { "problem.yaml", "statement/problem.vi.md", "input_validators/ojgen_validator/run", "input_validators/ojgen_validator/validator.py", "TEST_INVENTORY.csv" })
            if (!HasFile(root, required)) result.Errors.Add("Gói ICPC/Kattis thiếu " + required + ".");
        if (HasFile(root, "problem.yaml"))
        {
            string yaml = File.ReadAllText(Path.Combine(root, "problem.yaml"));
            if (!yaml.Contains("problem_format_version: \"2025-09\"", StringComparison.Ordinal)) result.Errors.Add("problem.yaml thiếu problem_format_version 2025-09.");
            if (!yaml.Contains("type: pass-fail", StringComparison.Ordinal)) result.Errors.Add("problem.yaml phải khai báo type: pass-fail.");
            Match uuid = Regex.Match(yaml, @"(?m)^uuid:\s*([0-9a-fA-F-]{36})\s*$");
            if (!uuid.Success || !Guid.TryParse(uuid.Groups[1].Value, out _)) result.Errors.Add("UUID trong problem.yaml không hợp lệ.");
            if (!yaml.Contains("time_limit:", StringComparison.Ordinal) || !yaml.Contains("memory:", StringComparison.Ordinal)) result.Errors.Add("problem.yaml thiếu giới hạn thời gian hoặc bộ nhớ.");
        }
        string secret = Path.Combine(root, "data", "secret");
        if (!Directory.Exists(secret) || FileManager.ListFilesWithExt(secret, ".in").Count == 0)
            result.Errors.Add("Gói ICPC/Kattis cần ít nhất một test data/secret/*.in.");
        else
        {
            var inputs = FileManager.ListFilesWithExt(secret, ".in");
            foreach (string input in inputs)
                if (!File.Exists(Path.ChangeExtension(input, ".ans"))) result.Errors.Add("Test bí mật thiếu .ans: " + Path.GetFileName(input) + ".");
            if (FileManager.ListFilesWithExt(secret, ".ans").Count != inputs.Count) result.Errors.Add("Số file .in/.ans trong data/secret không bằng nhau.");
        }
        string samples = Path.Combine(root, "data", "sample");
        if (Directory.Exists(samples))
        {
            var inputs = FileManager.ListFilesWithExt(samples, ".in");
            foreach (string input in inputs)
                if (!File.Exists(Path.ChangeExtension(input, ".ans"))) result.Errors.Add("Sample thiếu .ans: " + Path.GetFileName(input) + ".");
            if (FileManager.ListFilesWithExt(samples, ".ans").Count != inputs.Count) result.Errors.Add("Số file sample .in/.ans không bằng nhau.");
        }
        if (!Directory.Exists(Path.Combine(root, "submissions", "accepted")) ||
            !Directory.EnumerateFiles(Path.Combine(root, "submissions", "accepted"), "*", SearchOption.TopDirectoryOnly).Any())
            result.Errors.Add("Gói ICPC/Kattis cần lời giải trong submissions/accepted.");
    }
}
