using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OjGen.Core;

public sealed class TestInventoryRow
{
    public string Name { get; set; } = "";
    public string InputPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string Group { get; set; } = "";
    public string Tags { get; set; } = "";
    public long InputBytes { get; set; }
    public long? OutputBytes { get; set; }
    public double? NumericMin { get; set; }
    public double? NumericMax { get; set; }
    public string DuplicateOf { get; set; } = "";
    public string Validation { get; set; } = "Chưa chạy";
}

/// <summary>Inventory and safe batch operations for an existing test directory.</summary>
public static partial class TestInventoryService
{
    public static List<TestInventoryRow> Read(string directory)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Không tìm thấy thư mục test.");
        var metadata = ReadMetadata(directory);
        var rows = new List<TestInventoryRow>();
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string input in FileManager.ListFilesWithExt(directory, ".inp"))
        {
            string stem = Path.GetFileNameWithoutExtension(input);
            string output = Path.ChangeExtension(input, ".out");
            string hash = Hash(input);
            var nums = NumberRegex().Matches(File.ReadAllText(input, Encoding.UTF8)).Cast<System.Text.RegularExpressions.Match>()
                .Select(match => double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? (double?)value : null)
                .Where(value => value.HasValue).Select(value => value!.Value).ToList();
            bool hasMetadata = metadata.TryGetValue(stem, out var item);
            rows.Add(new TestInventoryRow
            {
                Name = stem,
                InputPath = input,
                OutputPath = output,
                Group = hasMetadata ? item.Group : "",
                Tags = hasMetadata ? string.Join(", ", item.Tags) : "",
                InputBytes = new FileInfo(input).Length,
                OutputBytes = File.Exists(output) ? new FileInfo(output).Length : null,
                NumericMin = nums.Count == 0 ? null : nums.Min(),
                NumericMax = nums.Count == 0 ? null : nums.Max(),
                DuplicateOf = hashes.TryGetValue(hash, out string? previous) ? previous : "",
            });
            hashes.TryAdd(hash, stem);
        }
        return rows;
    }

    public static void Rename(string directory, IReadOnlyList<TestInventoryRow> rows, string prefix)
    {
        if (rows.Count == 0) throw new InvalidOperationException("Chọn ít nhất một test để đổi tên.");
        string safePrefix = SafeStem(prefix);
        if (safePrefix.Length == 0) throw new InvalidDataException("Tiền tố tên file không hợp lệ.");
        var allInputs = FileManager.ListFilesWithExt(directory, ".inp");
        var selected = rows.Select(row => Path.GetFullPath(row.InputPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ordered = allInputs.Where(path => selected.Contains(Path.GetFullPath(path))).ToList();
        if (ordered.Count != rows.Count) throw new InvalidOperationException("Một số test đã bị xóa hoặc đổi tên ở ngoài OjGen. Hãy tải lại danh sách.");
        string provenancePath = Path.Combine(directory, "ojgen_provenance.json");
        string checksumPath = Path.Combine(directory, FileManager.ChecksumFile);
        string inventoryPath = Path.Combine(directory, "TEST_INVENTORY.csv");
        byte[]? provenanceBackup = File.Exists(provenancePath) ? File.ReadAllBytes(provenancePath) : null;
        byte[]? checksumBackup = File.Exists(checksumPath) ? File.ReadAllBytes(checksumPath) : null;
        byte[]? inventoryBackup = File.Exists(inventoryPath) ? File.ReadAllBytes(inventoryPath) : null;
        int width = FileManager.TestNumberWidth(ordered.Count);
        var targets = ordered.Select((path, i) => Path.Combine(directory, safePrefix + (i + 1).ToString("D" + width, CultureInfo.InvariantCulture) + ".inp")).ToList();
        foreach (string target in targets)
            if ((File.Exists(target) && !selected.Contains(Path.GetFullPath(target))) ||
                (File.Exists(Path.ChangeExtension(target, ".out")) && !selected.Contains(Path.GetFullPath(Path.ChangeExtension(target, ".inp")))))
                throw new IOException("Tên mới trùng với test chưa chọn: " + Path.GetFileName(target));

        var staged = new List<(string InputOriginal, string? OutputOriginal, string InputTemp, string? OutputTemp, string InputTarget, string? OutputTarget)>();
        try
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                string oldInput = ordered[i], oldOutput = Path.ChangeExtension(oldInput, ".out");
                string stem = Path.GetFileNameWithoutExtension(targets[i]);
                string newOutput = Path.Combine(directory, stem + ".out");
                string tempInput = oldInput + ".ojgen-rename-" + Guid.NewGuid().ToString("N");
                string? tempOutput = File.Exists(oldOutput) ? oldOutput + ".ojgen-rename-" + Guid.NewGuid().ToString("N") : null;
                staged.Add((oldInput, File.Exists(oldOutput) ? oldOutput : null, tempInput, tempOutput, targets[i], tempOutput == null ? null : newOutput));
                File.Move(oldInput, tempInput);
                if (tempOutput != null) File.Move(oldOutput, tempOutput);
            }
            foreach (var item in staged)
            {
                File.Move(item.InputTemp, item.InputTarget);
                if (item.OutputTemp != null && item.OutputTarget != null) File.Move(item.OutputTemp, item.OutputTarget);
            }
            UpdateMetadata(directory, ordered.Select((path, i) => (Name: Path.GetFileNameWithoutExtension(path), NewName: Path.GetFileNameWithoutExtension(targets[i])))
                .ToDictionary(item => item.Name, item => item.NewName, StringComparer.OrdinalIgnoreCase));
            if (!FileManager.WriteTestChecksumManifest(directory, out _, out string? checksumError))
                throw new IOException(checksumError ?? "Không thể cập nhật manifest SHA-256.");
            WriteCsv(directory);
        }
        catch
        {
            foreach (var item in staged)
            {
                try { if (File.Exists(item.InputTarget) && !File.Exists(item.InputTemp)) File.Move(item.InputTarget, item.InputTemp); } catch { }
                try { if (item.OutputTarget != null && item.OutputTemp != null && File.Exists(item.OutputTarget) && !File.Exists(item.OutputTemp)) File.Move(item.OutputTarget, item.OutputTemp); } catch { }
                try { if (File.Exists(item.InputTemp) && item.InputOriginal.Length > 0) File.Move(item.InputTemp, item.InputOriginal, true); } catch { }
                try { if (item.OutputOriginal != null && item.OutputTemp != null && File.Exists(item.OutputTemp)) File.Move(item.OutputTemp, item.OutputOriginal, true); } catch { }
            }
            RestoreFile(provenancePath, provenanceBackup);
            RestoreFile(checksumPath, checksumBackup);
            RestoreFile(inventoryPath, inventoryBackup);
            throw;
        }
    }

    public static void AssignGroup(string directory, IReadOnlyList<TestInventoryRow> rows, string group, IReadOnlyList<string> tags)
    {
        if (rows.Count == 0) throw new InvalidOperationException("Chọn ít nhất một test để đổi nhóm.");
        group = (group ?? "").Trim();
        if (group.Length == 0 || group.Length > 100) throw new InvalidDataException("Tên nhóm phải dài 1–100 ký tự.");
        var root = LoadProvenance(directory);
        if (root == null)
        {
            root = new JsonObject { ["schemaVersion"] = 1, ["tests"] = new JsonArray() };
            var allRows = Read(directory);
            foreach (var row in allRows)
            {
                var input = new FileInfo(row.InputPath);
                var item = new JsonObject
                {
                    ["name"] = row.Name,
                    ["group"] = "",
                    ["tags"] = new JsonArray(),
                    ["input"] = new JsonObject { ["path"] = Path.GetFileName(row.InputPath), ["bytes"] = input.Length, ["sha256"] = Hash(row.InputPath) },
                    ["output"] = File.Exists(row.OutputPath) ? new JsonObject { ["path"] = Path.GetFileName(row.OutputPath), ["bytes"] = new FileInfo(row.OutputPath).Length, ["sha256"] = Hash(row.OutputPath) } : null,
                };
                (root["tests"] as JsonArray)!.Add(item);
            }
        }
        if (root["tests"] is not JsonArray tests) throw new InvalidOperationException("Provenance hiện có không chứa danh sách test hợp lệ.");
        var selected = rows.Select(row => row.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (JsonNode? node in tests)
        {
            if (node is not JsonObject test) continue;
            string name = test["name"]?.GetValue<string>() ?? "";
            if (!selected.Contains(name)) continue;
            test["group"] = group;
            var tagArray = new JsonArray();
            foreach (string tag in tags) tagArray.Add(tag);
            test["tags"] = tagArray;
        }
        SaveProvenance(directory, root!);
        WriteCsv(directory);
    }

    public static List<TestInventoryRow> Validate(string directory, Problem problem, Config config)
    {
        var rows = Read(directory);
        var groupConfigs = ReadGroupConfigs(directory, problem);
        foreach (var row in rows)
        {
            try
            {
                string input = File.ReadAllText(row.InputPath, new UTF8Encoding(false, true));
                Config effective = groupConfigs.TryGetValue(row.Group, out var saved) ? saved : config;
                row.Validation = problem.ValidateInput(input, effective) ?? "Hợp lệ";
            }
            catch (Exception ex) { row.Validation = "Lỗi đọc: " + ex.Message; }
        }
        return rows;
    }

    public static void ExportCsv(string path, IReadOnlyList<TestInventoryRow> rows)
    {
        static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("name,group,tags,input_bytes,output_bytes,numeric_min,numeric_max,duplicate_of,validation\n");
        foreach (var row in rows)
            sb.Append(Q(row.Name)).Append(',').Append(Q(row.Group)).Append(',').Append(Q(row.Tags)).Append(',')
                .Append(row.InputBytes).Append(',').Append(row.OutputBytes?.ToString(CultureInfo.InvariantCulture) ?? "")
                .Append(',').Append(row.NumericMin?.ToString("R", CultureInfo.InvariantCulture) ?? "")
                .Append(',').Append(row.NumericMax?.ToString("R", CultureInfo.InvariantCulture) ?? "")
                .Append(',').Append(Q(row.DuplicateOf)).Append(',').Append(Q(row.Validation)).Append('\n');
        AtomicFile.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteCsv(string directory) => ExportCsv(Path.Combine(directory, "TEST_INVENTORY.csv"), Read(directory));

    private static string SafeStem(string value) => new((value ?? "").Trim().Select(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_').ToArray());
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static JsonObject? LoadProvenance(string directory)
    {
        string path = Path.Combine(directory, "ojgen_provenance.json");
        if (!File.Exists(path)) return null;
        try { return JsonNode.Parse(File.ReadAllText(path)) as JsonObject; } catch (JsonException) { return null; }
    }
    private static Dictionary<string, (string Group, List<string> Tags)> ReadMetadata(string directory)
    {
        var result = new Dictionary<string, (string, List<string>)>(StringComparer.OrdinalIgnoreCase);
        var root = LoadProvenance(directory);
        if (root?["tests"] is not JsonArray tests) return result;
        foreach (JsonNode? node in tests)
        {
            if (node is not JsonObject test) continue;
            string name = test["name"]?.GetValue<string>() ?? "";
            string group = test["group"]?.GetValue<string>() ?? "";
            var tags = test["tags"] is JsonArray array ? array.Select(item => item?.GetValue<string>() ?? "").Where(tag => tag.Length > 0).ToList() : new();
            if (name.Length > 0) result[name] = (group, tags);
        }
        return result;
    }
    private static Dictionary<string, Config> ReadGroupConfigs(string directory, Problem problem)
    {
        var result = new Dictionary<string, Config>(StringComparer.OrdinalIgnoreCase);
        var root = LoadProvenance(directory);
        if (root == null) return result;
        if (root["groups"] is JsonArray groups)
            foreach (JsonNode? node in groups)
                if (node is JsonObject group && group["name"]?.GetValue<string>() is { Length: > 0 } name)
                    result[name] = ParseConfig(group["ints"] as JsonObject, group["texts"] as JsonObject, problem);
        if (root["manualTests"] is JsonArray manualTests)
            foreach (JsonNode? node in manualTests)
                if (node is JsonObject test && test["configName"]?.GetValue<string>() is { Length: > 0 } name)
                    result[name] = ParseConfig(test["ints"] as JsonObject, test["texts"] as JsonObject, problem);
        return result;
    }
    private static Config ParseConfig(JsonObject? ints, JsonObject? texts, Problem problem)
    {
        var config = problem.DefaultConfig();
        foreach (var pair in ints ?? new JsonObject())
            if (pair.Value != null && long.TryParse(pair.Value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)) config.Set(pair.Key, value);
        foreach (var pair in texts ?? new JsonObject()) if (pair.Value != null) config.SetText(pair.Key, pair.Value.GetValue<string>());
        return config;
    }
    private static void UpdateMetadata(string directory, IReadOnlyDictionary<string, string> renames)
    {
        var root = LoadProvenance(directory);
        if (root?["tests"] is not JsonArray tests) return;
        foreach (JsonNode? node in tests)
        {
            if (node is not JsonObject test) continue;
            string old = test["name"]?.GetValue<string>() ?? "";
            if (!renames.TryGetValue(old, out string? next)) continue;
            test["name"] = next;
            if (test["input"] is JsonObject input) { input["path"] = next + ".inp"; input["sha256"] = Hash(Path.Combine(directory, next + ".inp")); input["bytes"] = new FileInfo(Path.Combine(directory, next + ".inp")).Length; }
            if (test["output"] is JsonObject output)
            {
                string outputPath = Path.Combine(directory, next + ".out");
                if (File.Exists(outputPath)) { output["path"] = next + ".out"; output["sha256"] = Hash(outputPath); output["bytes"] = new FileInfo(outputPath).Length; }
            }
        }
        SaveProvenance(directory, root!);
    }
    private static void SaveProvenance(string directory, JsonObject root)
    {
        string path = Path.Combine(directory, "ojgen_provenance.json");
        AtomicFile.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    }

    private static void RestoreFile(string path, byte[]? contents)
    {
        try
        {
            if (contents == null) { if (File.Exists(path)) File.Delete(path); return; }
            string temp = path + ".rollback.tmp";
            File.WriteAllBytes(temp, contents);
            File.Move(temp, path, true);
        }
        catch { }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial System.Text.RegularExpressions.Regex NumberRegex();
}
