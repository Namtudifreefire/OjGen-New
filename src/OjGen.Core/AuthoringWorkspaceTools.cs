using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OjGen.Core;

/// <summary>Reusable authoring defaults. Files are data only and never execute code.</summary>
public sealed class AuthoringPreset
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public string ProblemId { get; set; } = "";
    public string ProblemName { get; set; } = "";
    public string ProblemCode { get; set; } = "";
    public string ReferenceSolution { get; set; } = "";
    public int TestCount { get; set; } = 10;
    public string SeedPolicy { get; set; } = "Random";
    public ulong? Seed { get; set; }
    public TestPlanProfile TestPlan { get; set; } = new();
    public string ValidatorPath { get; set; } = "";
    public string CheckerPath { get; set; } = "";
    public string CheckerArguments { get; set; } = "{input} {output} {answer}";
    public int CheckerTimeoutMs { get; set; }
    public string ContractSuitePath { get; set; } = "";
    public string CandidateSolution { get; set; } = "";
    public List<string> RequiredCoverageTags { get; set; } = new();
    public Dictionary<string, long> Ints { get; set; } = new();
    public Dictionary<string, string> Texts { get; set; } = new();

    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException($"Không hỗ trợ preset phiên bản {Version}.");
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Preset cần có tên.");
        var problem = Problems.Find(ProblemId) ?? throw new InvalidDataException("Không tìm thấy khuôn bài trong preset: " + ProblemId);
        if (TestCount < 1 || TestCount > Generator.MaxTests) throw new InvalidDataException($"Số test phải trong [1, {Generator.MaxTests}].");
        if (SeedPolicy is not ("Random" or "Fixed")) throw new InvalidDataException("SeedPolicy chỉ nhận Random hoặc Fixed.");
        if (SeedPolicy == "Fixed" && Seed == null) throw new InvalidDataException("Preset seed cố định cần giá trị seed.");
        if (CheckerTimeoutMs is < 0 or > 3600000) throw new InvalidDataException("Timeout checker không hợp lệ.");
        TestPlan ??= new TestPlanProfile();
        Ints ??= new Dictionary<string, long>();
        Texts ??= new Dictionary<string, string>();
        if (TestPlan.Groups?.Count > 0) _ = TestPlan.ToGroups(problem);
        if (TestCoverageTags.Parse(string.Join(',', RequiredCoverageTags ?? new()), out var tags) is { } error)
            throw new InvalidDataException(error);
        RequiredCoverageTags = tags;
    }

    public static AuthoringPreset Load(string path)
    {
        var preset = JsonSerializer.Deserialize<AuthoringPreset>(File.ReadAllText(path), JsonOptions())
            ?? throw new InvalidDataException("Preset rỗng hoặc sai JSON.");
        preset.Validate();
        return preset;
    }

    public void Save(string path)
    {
        Validate();
        string full = Path.GetFullPath(path);
        AtomicFile.WriteAllText(full, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
    }

    internal static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };
}

public static class AuthoringPresetStore
{
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OjGen", "Presets");
    public static IReadOnlyList<string> List(string? directory = null)
    {
        string root = Path.GetFullPath(directory ?? DefaultDirectory);
        if (!Directory.Exists(root)) return Array.Empty<string>();
        return Directory.EnumerateFiles(root, "*.ojpreset.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }
    public static string Save(AuthoringPreset preset, string? directory = null)
    {
        string root = Path.GetFullPath(directory ?? DefaultDirectory);
        Directory.CreateDirectory(root);
        string fileName = new(preset.Name.Trim().Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || char.IsControl(ch) ? '_' : ch).ToArray());
        fileName = fileName.Trim().Trim('.', ' ');
        if (fileName.Length == 0) throw new InvalidDataException("Tên preset không tạo được tên file hợp lệ.");
        string path = Path.Combine(root, fileName + ".ojpreset.json");
        preset.Save(path);
        return path;
    }
}

public sealed class ContestWorkspaceDocument
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public int DurationMinutes { get; set; } = 180;
    public List<ContestProblemEntry> Problems { get; set; } = new();

    public static ContestWorkspaceDocument Load(string path)
    {
        var document = JsonSerializer.Deserialize<ContestWorkspaceDocument>(File.ReadAllText(path), AuthoringPreset.JsonOptions())
            ?? throw new InvalidDataException("Contest workspace rỗng hoặc sai JSON.");
        document.Validate();
        return document;
    }
    public void Save(string path)
    {
        Validate();
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
    }

    public string ExportArchive(string destinationZip)
    {
        Validate();
        string full = Path.GetFullPath(destinationZip);
        if (!full.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Contest archive cần phần mở rộng .zip.");
        if (File.Exists(full) || Directory.Exists(full)) throw new IOException("Contest archive đích đã tồn tại.");
        foreach (var item in Problems)
            if (string.IsNullOrWhiteSpace(item.PackagePath)) throw new InvalidDataException($"Bài {item.Code} chưa có PackagePath.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        try
        {
            using var target = ZipFile.Open(full, ZipArchiveMode.Create);
            int files = 0;
            foreach (var item in Problems.OrderBy(item => item.Order))
            {
                string source = Path.GetFullPath(item.PackagePath);
                string prefix = "problems/" + item.Code + "/";
                if (Directory.Exists(source))
                {
                    if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Không đưa symbolic link package vào contest archive: " + source);
                    foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Package chứa symbolic link: " + directory);
                    foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                    {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Package chứa symbolic link: " + file);
                        string relative = Path.GetRelativePath(source, file).Replace(Path.DirectorySeparatorChar, '/');
                        CopyToArchive(target, file, prefix + relative);
                        files++;
                    }
                }
                else if (File.Exists(source) && string.Equals(Path.GetExtension(source), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var package = ZipFile.OpenRead(source);
                    foreach (var entry in package.Entries.Where(entry => !entry.FullName.EndsWith('/')))
                    {
                        string relative = entry.FullName.Replace('\\', '/');
                        if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is ".." or ".") || relative.Contains(':'))
                            throw new InvalidDataException($"Package {item.Code} chứa đường dẫn không an toàn: {relative}");
                        var output = target.CreateEntry(prefix + relative, CompressionLevel.Optimal);
                        using var input = entry.Open(); using var stream = output.Open(); input.CopyTo(stream); files++;
                    }
                }
                else throw new FileNotFoundException($"Không tìm thấy package cho bài {item.Code}.", source);
            }
            if (files == 0) throw new InvalidDataException("Không có file package nào để đóng gói.");
            var manifest = target.CreateEntry("contest.package.json", CompressionLevel.Optimal);
            using var writer = new StreamWriter(manifest.Open(), new System.Text.UTF8Encoding(false));
            writer.Write(JsonSerializer.Serialize(new
            {
                Version = 1, Name, DurationMinutes,
                Problems = Problems.OrderBy(item => item.Order).Select(item => new
                {
                    item.Code, item.Order, item.TimeLimitMinutes, PackagePath = "problems/" + item.Code + "/",
                }),
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            try { if (File.Exists(full)) File.Delete(full); } catch { }
            throw;
        }
        return full;
    }

    private static void CopyToArchive(ZipArchive archive, string source, string entryName)
    {
        string normalized = entryName.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(part => part is ".." or ".") || normalized.Contains(':'))
            throw new InvalidDataException("Đường dẫn package không an toàn: " + normalized);
        var entry = archive.CreateEntry(normalized, CompressionLevel.Optimal);
        using var input = File.OpenRead(source); using var output = entry.Open(); input.CopyTo(output);
    }
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Phiên bản contest workspace không được hỗ trợ.");
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Contest cần có tên.");
        if (DurationMinutes is < 1 or > 24 * 60) throw new InvalidDataException("Thời lượng contest phải trong [1, 1440] phút.");
        if (Problems == null || Problems.Count == 0) throw new InvalidDataException("Contest cần ít nhất một bài.");
        if (Problems.Any(item => string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.ProjectPath)))
            throw new InvalidDataException("Mỗi bài cần mã bài và đường dẫn project.");
        if (Problems.Any(item => item.Code.Trim().Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_'))))
            throw new InvalidDataException("Mã bài contest chỉ được chứa chữ ASCII, số, dấu gạch ngang và gạch dưới.");
        if (Problems.Any(item => item.Code != item.Code.Trim())) throw new InvalidDataException("Mã bài contest không được có khoảng trắng đầu/cuối.");
        if (Problems.Select(item => item.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Problems.Count)
            throw new InvalidDataException("Mã bài trong contest bị trùng.");
        if (Problems.Select(item => item.Order).Distinct().Count() != Problems.Count || Problems.Any(item => item.Order < 1))
            throw new InvalidDataException("Thứ tự bài phải là số dương và không trùng.");
        if (Problems.Any(item => item.TimeLimitMinutes is < 1 or > 1440)) throw new InvalidDataException("Thời lượng từng bài phải trong [1, 1440] phút.");
    }
}

public sealed class ContestProblemEntry
{
    public string Code { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string PackagePath { get; set; } = "";
    public int Order { get; set; }
    public int TimeLimitMinutes { get; set; } = 180;
}

public sealed record ProblemLibraryEntry(string Path, string Kind, string Name, string Code, string ProblemId, DateTime LastWriteUtc, IReadOnlyList<string> Tags);

/// <summary>Indexes authoring files under an explicitly selected local library root.</summary>
public static class ProblemLibrary
{
    public static IReadOnlyList<ProblemLibraryEntry> Scan(string root, string? query = null, string? tagFilter = null, int maxFiles = 100000)
    {
        string fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot)) throw new DirectoryNotFoundException("Không tìm thấy thư viện đề: " + fullRoot);
        var result = new List<ProblemLibraryEntry>();
        int scanned = 0;
        string[] patterns = { "*.ojproject", "*.ojdraft.json", "problem.ojproblem.json" };
        foreach (string pattern in patterns)
        {
            foreach (string path in Directory.EnumerateFiles(fullRoot, pattern, SearchOption.AllDirectories))
            {
                if (++scanned > maxFiles) throw new InvalidDataException($"Thư viện vượt quá giới hạn {maxFiles} file.");
                try
                {
                    using var json = JsonDocument.Parse(File.ReadAllText(path));
                    var rootElement = json.RootElement;
                    string name = ReadString(rootElement, "ProblemName", "Name");
                    string code = ReadString(rootElement, "ProblemCode", "Code");
                    string id = ReadString(rootElement, "ProblemId");
                    string kind = pattern == "*.ojproject" ? "project" : pattern == "*.ojdraft.json" ? "statement" : "problem profile";
                    string statementCode = ReadString(rootElement, "Code");
                    if (code.Length == 0) code = statementCode;
                    if (name.Length == 0) name = ReadString(rootElement, "Name");
                    var tags = ReadPlanTags(rootElement);
                    var entry = new ProblemLibraryEntry(path, kind, name, code, id, File.GetLastWriteTimeUtc(path), tags);
                    string haystack = string.Join(' ', entry.Name, entry.Code, entry.ProblemId, entry.Kind, path, string.Join(' ', entry.Tags));
                    bool matchesQuery = string.IsNullOrWhiteSpace(query) || haystack.Contains(query, StringComparison.OrdinalIgnoreCase);
                    bool matchesTag = string.IsNullOrWhiteSpace(tagFilter) || entry.Tags.Contains(tagFilter, StringComparer.OrdinalIgnoreCase);
                    if (matchesQuery && matchesTag) result.Add(entry);
                }
                catch (JsonException) { }
            }
        }
        return result.OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (string name in names)
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
        return "";
    }

    private static IReadOnlyList<string> ReadPlanTags(JsonElement root)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string propertyName in new[] { "TestPlan", "LastGeneratedPlan" })
        {
            if (!root.TryGetProperty(propertyName, out var plan) || plan.ValueKind != JsonValueKind.Object) continue;
            AddTags(plan, "RequiredCoverageTags", tags);
            if (plan.TryGetProperty("Groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
                foreach (var group in groups.EnumerateArray()) AddTags(group, "CoverageTags", tags);
        }
        return tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddTags(JsonElement parent, string propertyName, HashSet<string> output)
    {
        if (parent.TryGetProperty(propertyName, out var values) && values.ValueKind == JsonValueKind.Array)
            foreach (var item in values.EnumerateArray()) if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())) output.Add(item.GetString()!.Trim());
    }

    public static string CloneProject(string source, string destination)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("Không tìm thấy project cần nhân bản.", source);
        if (!string.Equals(Path.GetExtension(source), ".ojproject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Chỉ có thể nhân bản file .ojproject.");
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Đích nhân bản đã tồn tại.");
        string full = Path.GetFullPath(destination);
        if (!string.Equals(Path.GetExtension(full), ".ojproject", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Bản sao cần có phần mở rộng .ojproject.");
        using (var parsed = JsonDocument.Parse(File.ReadAllText(source)))
        {
            if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                !parsed.RootElement.TryGetProperty("Version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int versionNumber) || versionNumber != 1)
                throw new InvalidDataException("File nguồn không phải .ojproject phiên bản được hỗ trợ.");
            if (!parsed.RootElement.TryGetProperty("ProblemId", out var id) || id.ValueKind != JsonValueKind.String || Problems.Find(id.GetString() ?? "") == null)
                throw new InvalidDataException("Project nguồn dùng khuôn bài không có trong phiên bản OjGen hiện tại.");
        }
        JsonNode document = JsonNode.Parse(File.ReadAllText(source)) ?? throw new InvalidDataException("Project rỗng hoặc JSON lỗi.");
        document["ProjectPath"] = full;
        document["LastTestDirectory"] = "";
        document["LastGeneratedCount"] = 0;
        document["LastGeneratedSeed"] = 0;
        document["LastGeneratedProblemId"] = "";
        document["LastGeneratedSolutionPath"] = "";
        document["LastGeneratedInts"] = new JsonObject();
        document["LastGeneratedTexts"] = new JsonObject();
        document["LastGeneratedPlan"] = new JsonObject { ["Version"] = 1, ["ProblemId"] = "", ["RequiredCoverageTags"] = new JsonArray(), ["Groups"] = new JsonArray() };
        document["LastGeneratedManualTests"] = new JsonArray();
        document["LastGeneratedCoverageTags"] = new JsonArray();
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        AtomicFile.WriteAllText(full, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
        return full;
    }
}

public sealed class ProblemVersionDiff
{
    public List<string> Added { get; } = new();
    public List<string> Removed { get; } = new();
    public List<string> Changed { get; } = new();
    public List<string> ReproducibilityRisks { get; } = new();
    public bool Same => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
}

public static class ProblemVersionComparer
{
    public static ProblemVersionDiff Compare(string left, string right)
    {
        var a = Files(left); var b = Files(right);
        var diff = new ProblemVersionDiff();
        foreach (string key in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            if (!a.TryGetValue(key, out string? first)) { diff.Added.Add(key); continue; }
            if (!b.TryGetValue(key, out string? second)) { diff.Removed.Add(key); continue; }
            if (!string.Equals(Hash(first), Hash(second), StringComparison.OrdinalIgnoreCase)) diff.Changed.Add(key);
        }
        if (diff.Added.Concat(diff.Removed).Concat(diff.Changed).Any(IsGeneratorOrTests))
            diff.ReproducibilityRisks.Add("Thay đổi input test, kế hoạch, tham số generator hoặc seed có thể làm mất khả năng tái tạo bộ test.");
        if (diff.Changed.Any(path => path.Contains("statement", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ojdraft.json", StringComparison.OrdinalIgnoreCase)))
            diff.ReproducibilityRisks.Add("Nội dung statement đã đổi; đối chiếu lại ví dụ, giới hạn và định dạng input/output.");
        return diff;
    }

    private static bool IsGeneratorOrTests(string path) => path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("plan", StringComparison.OrdinalIgnoreCase) || path.Contains("ojproblem", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".ojproject", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ojdraft.json", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> Files(string path)
    {
        string full = Path.GetFullPath(path);
        if (File.Exists(full)) return new(StringComparer.OrdinalIgnoreCase) { [Path.GetFileName(full)] = full };
        if (!Directory.Exists(full)) throw new FileNotFoundException("Không tìm thấy phiên bản hoặc thư mục.", full);
        return Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(full, file).Replace(Path.DirectorySeparatorChar, '/'), file => file, StringComparer.OrdinalIgnoreCase);
    }
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public sealed class StatementResourceReport
{
    public List<string> Missing { get; } = new();
    public List<string> Unsafe { get; } = new();
    public List<string> Remote { get; } = new();
    public bool IsValid => Missing.Count == 0 && Unsafe.Count == 0;
}

public static class StatementResourceManager
{
    private static readonly Regex ResourcePattern = new("""!\[[^\]]*\]\((?<md>[^)\s]+)(?:\s+[^)]*)?\)|<(?:img|source)[^>]+(?:src|href)=["'](?<html>[^"']+)["']|\[[^\]]+\]\((?<link>[^)\s]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static StatementResourceReport Check(string statementPath)
    {
        string full = Path.GetFullPath(statementPath);
        if (!File.Exists(full)) throw new FileNotFoundException("Không tìm thấy statement.", full);
        string root = Path.GetDirectoryName(full)!;
        return CheckContent(File.ReadAllText(full), root);
    }

    public static StatementResourceReport CheckContent(string text, string rootDirectory)
    {
        string root = Path.GetFullPath(rootDirectory);
        var report = new StatementResourceReport();
        foreach (Match match in ResourcePattern.Matches(text))
        {
            string target = match.Groups["md"].Success ? match.Groups["md"].Value : match.Groups["html"].Success ? match.Groups["html"].Value : match.Groups["link"].Value;
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme is "http" or "https") report.Remote.Add(target);
                else if (uri.Scheme != "mailto") report.Unsafe.Add(target);
                continue;
            }
            target = Uri.UnescapeDataString(target.Split('#', '?')[0]);
            if (target.Length == 0 || target.StartsWith('#')) continue;
            if (Path.IsPathRooted(target)) { report.Unsafe.Add(target); continue; }
            string resolved = Path.GetFullPath(Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar)));
            if (!resolved.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                report.Unsafe.Add(target);
            else if (HasReparsePoint(resolved, root)) report.Unsafe.Add(target);
            else if (!File.Exists(resolved)) report.Missing.Add(target);
        }
        return report;
    }

    private static bool HasReparsePoint(string path, string root)
    {
        if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return true;
        string current = path;
        while (current.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            string? parent = Path.GetDirectoryName(current);
            if (parent == null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
        return false;
    }
}

public sealed class TranslationDocument
{
    public int Version { get; set; } = 1;
    public string SourceStatement { get; set; } = "";
    public List<TranslationEntry> Translations { get; set; } = new();
    public static TranslationDocument Load(string path) => JsonSerializer.Deserialize<TranslationDocument>(File.ReadAllText(path), AuthoringPreset.JsonOptions())
        ?? throw new InvalidDataException("Tệp dịch rỗng hoặc sai JSON.");
    public void Save(string path) => AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
}

public sealed class TranslationEntry
{
    public string Locale { get; set; } = "";
    public string StatementPath { get; set; } = "";
}

public sealed class TranslationAuditReport
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Passed => Errors.Count == 0;
}

/// <summary>Checks linked localized statement drafts for missing fields and sample/limit drift.</summary>
public static class TranslationManager
{
    private static readonly string[] RequiredFields = { "Description", "Input", "Output", "Constraints", "Examples" };
    public static TranslationAuditReport Check(string manifestPath)
    {
        string root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var manifest = TranslationDocument.Load(manifestPath);
        var report = new TranslationAuditReport();
        if (manifest.Version != 1 || string.IsNullOrWhiteSpace(manifest.SourceStatement)) { report.Errors.Add("Manifest dịch cần Version=1 và SourceStatement."); return report; }
        string sourcePath = Resolve(root, manifest.SourceStatement);
        if (!File.Exists(sourcePath)) { report.Errors.Add("Không tìm thấy statement gốc: " + manifest.SourceStatement); return report; }
        var source = ReadStatement(sourcePath);
        var locales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var translation in manifest.Translations ?? new())
        {
            if (string.IsNullOrWhiteSpace(translation.Locale) || string.IsNullOrWhiteSpace(translation.StatementPath))
            { report.Errors.Add("Mỗi bản dịch cần Locale và StatementPath."); continue; }
            if (!locales.Add(translation.Locale.Trim())) { report.Errors.Add("Locale bị lặp: " + translation.Locale); continue; }
            string localizedPath = Resolve(root, translation.StatementPath);
            if (!File.Exists(localizedPath)) { report.Errors.Add($"[{translation.Locale}] Không tìm thấy: {translation.StatementPath}"); continue; }
            var localized = ReadStatement(localizedPath);
            foreach (string field in RequiredFields)
                if (string.IsNullOrWhiteSpace(localized.GetValueOrDefault(field))) report.Errors.Add($"[{translation.Locale}] Chưa dịch trường {field}.");
            if (source.TryGetValue("TimeLimitMs", out string? sourceTime) && localized.TryGetValue("TimeLimitMs", out string? translatedTime) && sourceTime != translatedTime)
                report.Errors.Add($"[{translation.Locale}] TimeLimitMs khác bản gốc.");
            if (source.TryGetValue("MemoryLimitMb", out string? sourceMemory) && localized.TryGetValue("MemoryLimitMb", out string? translatedMemory) && sourceMemory != translatedMemory)
                report.Errors.Add($"[{translation.Locale}] MemoryLimitMb khác bản gốc.");
            if (source.TryGetValue("Examples", out string? sourceExamples) && localized.TryGetValue("Examples", out string? translatedExamples) &&
                CodeBlocks(sourceExamples) != CodeBlocks(translatedExamples))
                report.Errors.Add($"[{translation.Locale}] Input/output trong code block ví dụ khác bản gốc.");
            if (source.TryGetValue("Constraints", out string? sourceConstraints) && localized.TryGetValue("Constraints", out string? translatedConstraints) &&
                string.Join(',', Regex.Matches(sourceConstraints, @"\d+").Select(match => match.Value)) !=
                string.Join(',', Regex.Matches(translatedConstraints, @"\d+").Select(match => match.Value)))
                report.Warnings.Add($"[{translation.Locale}] Các con số trong giới hạn khác bản gốc; xác minh đây là dịch đúng, không phải giới hạn bị đổi.");
        }
        if (manifest.Translations == null || manifest.Translations.Count == 0) report.Warnings.Add("Manifest chưa liên kết bản dịch nào.");
        return report;
    }

    private static string Resolve(string root, string relative)
    {
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Đường dẫn statement dịch phải nằm trong thư mục manifest.");
        return full;
    }
    private static Dictionary<string, string> ReadStatement(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string field in RequiredFields.Concat(new[] { "TimeLimitMs", "MemoryLimitMb" }))
            if (json.RootElement.TryGetProperty(field, out var value)) values[field] = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        return values;
    }
    private static string CodeBlocks(string text) => string.Join("\n", Regex.Matches(text, "```[^\\n]*\\n(?<code>.*?)```", RegexOptions.Singleline).Select(match =>
        string.Join(" ", match.Groups["code"].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))));
}

/// <summary>High-level compatibility hints; import still goes through the safe extension-based test importer.</summary>
public static class ProblemFormatCompatibility
{
    public static IReadOnlyList<string> SupportedTestFormats => new[] { "OjGen (.inp/.out)", "Polygon/Testlib data interchange (.in/.ans, .in/.out)", "DOMjudge / PPF legacy test data (.in/.ans)", "DMOJ flat test-data interchange (.in/.out)" };
    public static string CompatibilityNote(string format) => format.ToLowerInvariant() switch
    {
        "kattis-2025-09" or "icpc-2025-09" => "Release candidate 2025-09; target judge support is not universal. Validate against the target system.",
        "kattis-legacy" or "legacy-icpc" => "Stable legacy PPF subset. Confirm the target judge's supported subset before upload.",
        "polygon" or "testlib" or "domjudge" or "dmoj" => "Test data can be normalized to OjGen .inp/.out; generator/checker metadata, scoring and judging semantics are not imported automatically.",
        _ => "Unknown format. Use OjGen test import and verify metadata manually.",
    };
}
