using System.IO.Compression;
using System.Security.Cryptography;

namespace OjGen.Core;

public sealed class TestImportResult
{
    public int Inputs { get; init; }
    public int Outputs { get; init; }
    public List<string> Warnings { get; init; } = new();
}

public sealed class PackageDiffResult
{
    public int Identical { get; init; }
    public List<string> Added { get; init; } = new();
    public List<string> Removed { get; init; } = new();
    public List<string> Changed { get; init; } = new();
    public bool Same => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
}

/// <summary>Import and content-address comparison helpers for test folders and OJ archives.</summary>
public static class TestPackageTools
{
    private const int MaxTests = 10000;
    private const long MaxTotalBytes = 2L * 1024 * 1024 * 1024;
    private static readonly HashSet<string> InputExtensions = new(StringComparer.OrdinalIgnoreCase) { ".inp", ".in" };
    private static readonly HashSet<string> OutputExtensions = new(StringComparer.OrdinalIgnoreCase) { ".out", ".ans" };

    private static string Stem(string path) => Path.GetFileNameWithoutExtension(path);

    private static string TestIdentity(string relativePath)
    {
        string[] parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new InvalidDataException("Đường dẫn test rỗng trong package.");
        string stem = Path.GetFileNameWithoutExtension(parts[^1]);
        int dataIndex = Array.FindIndex(parts, part => string.Equals(part, "data", StringComparison.OrdinalIgnoreCase));
        if (dataIndex >= 0 && dataIndex + 1 < parts.Length &&
            (string.Equals(parts[dataIndex + 1], "sample", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(parts[dataIndex + 1], "secret", StringComparison.OrdinalIgnoreCase)))
        {
            string groupPath = string.Join("/", parts.Skip(dataIndex + 2).Take(parts.Length - dataIndex - 3).Append(stem));
            return parts[dataIndex + 1].ToLowerInvariant() + "/" + groupPath;
        }
        int categoryIndex = Array.FindIndex(parts, part => string.Equals(part, "sample", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(part, "secret", StringComparison.OrdinalIgnoreCase));
        if (categoryIndex >= 0)
        {
            string groupPath = string.Join("/", parts.Skip(categoryIndex + 1).Take(parts.Length - categoryIndex - 2).Append(stem));
            return parts[categoryIndex].ToLowerInvariant() + "/" + groupPath;
        }
        return parts.Length == 1 ? stem : string.Join("/", parts[..^1].Append(stem));
    }

    private static string CanonicalTestKey(string relativePath, bool input) => TestIdentity(relativePath) + (input ? ".inp" : ".out");
    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static Dictionary<string, string> ListTests(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                string ext = Path.GetExtension(entry.FullName);
                if (!InputExtensions.Contains(ext) && !OutputExtensions.Contains(ext)) continue;
                string key = CanonicalTestKey(entry.FullName, InputExtensions.Contains(ext));
                if (!result.TryAdd(key, "zip:" + path + "|" + entry.FullName))
                    throw new InvalidDataException("Có tên test trùng nhau trong gói: " + key);
            }
        }
        else if (Directory.Exists(path))
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file);
                if (!InputExtensions.Contains(ext) && !OutputExtensions.Contains(ext)) continue;
                string key = CanonicalTestKey(Path.GetRelativePath(path, file), InputExtensions.Contains(ext));
                if (!result.TryAdd(key, file)) throw new InvalidDataException("Có tên test trùng nhau sau khi rút gọn thư mục: " + key);
            }
        }
        else throw new FileNotFoundException("Không tìm thấy thư mục hoặc ZIP test.", path);
        return result;
    }

    public static TestImportResult Import(string source, string destination, string prefix = "test")
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-')))
            throw new InvalidDataException("Prefix chỉ được chứa chữ ASCII, số, '_' và '-'.");
        var items = ListTests(Path.GetFullPath(source));
        var inputs = items.Where(p => p.Key.EndsWith(".inp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToList();
        if (inputs.Count == 0) throw new InvalidDataException("Nguồn không có input .inp hoặc .in.");
        if (inputs.Count > MaxTests) throw new InvalidDataException($"Nguồn có quá {MaxTests} input.");
        long totalBytes = 0;
        foreach (var pair in items)
        {
            if (pair.Value.StartsWith("zip:", StringComparison.Ordinal))
            {
                string[] parts = pair.Value[4..].Split('|', 2);
                using var archive = ZipFile.OpenRead(parts[0]);
                var entry = archive.GetEntry(parts[1]) ?? throw new InvalidDataException("Không đọc được mục ZIP: " + parts[1]);
                totalBytes = checked(totalBytes + entry.Length);
            }
            else totalBytes = checked(totalBytes + new FileInfo(pair.Value).Length);
            if (totalBytes > MaxTotalBytes) throw new InvalidDataException("Tổng dữ liệu test vượt giới hạn import 2 GiB.");
        }
        string dest = Path.GetFullPath(destination);
        if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any())
            throw new IOException("Thư mục đích phải mới hoặc đang rỗng; không ghi đè test hiện có.");
        Directory.CreateDirectory(dest);
        int width = FileManager.TestNumberWidth(inputs.Count), outCount = 0;
        var warnings = new List<string>();
        using var zip = File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ZipFile.OpenRead(source) : null;
        for (int inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
        {
            var input = inputs[inputIndex];
            int index = inputIndex + 1;
            string name = FileManager.TestName(prefix, index, width);
            CopyItem(input.Value, Path.Combine(dest, name + ".inp"), zip);
            string outKey = input.Key[..^4] + ".out";
            if (items.TryGetValue(outKey, out string? output))
            {
                CopyItem(output, Path.Combine(dest, name + ".out"), zip);
                outCount++;
            }
            else warnings.Add($"Test nguồn '{Stem(input.Key)}' chưa có output .out/.ans.");
        }
        if (!FileManager.WriteTestChecksumManifest(dest, out _, out string? checksumError))
            throw new IOException(checksumError ?? "Không tạo được manifest SHA-256.");
        return new TestImportResult { Inputs = inputs.Count, Outputs = outCount, Warnings = warnings };
    }

    private static void CopyItem(string source, string target, ZipArchive? openArchive)
    {
        if (!source.StartsWith("zip:", StringComparison.Ordinal)) { File.Copy(source, target); return; }
        string[] parts = source[4..].Split('|', 2);
        ZipArchive archive = openArchive ?? ZipFile.OpenRead(parts[0]);
        bool ownsArchive = openArchive == null;
        try
        {
            var entry = archive.GetEntry(parts[1]) ?? throw new InvalidDataException("Không đọc được mục ZIP: " + parts[1]);
            using var input = entry.Open();
            using var output = File.Create(target);
            input.CopyTo(output);
        }
        finally { if (ownsArchive) archive.Dispose(); }
    }

    public static PackageDiffResult Compare(string left, string right)
    {
        var a = ListTests(Path.GetFullPath(left));
        var b = ListTests(Path.GetFullPath(right));
        var added = new List<string>();
        var removed = new List<string>();
        var changed = new List<string>();
        int identical = 0;
        foreach (string key in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            if (!a.TryGetValue(key, out string? first)) { added.Add(key); continue; }
            if (!b.TryGetValue(key, out string? second)) { removed.Add(key); continue; }
            string h1 = HashItem(first), h2 = HashItem(second);
            if (string.Equals(h1, h2, StringComparison.OrdinalIgnoreCase)) identical++;
            else changed.Add(key);
        }
        return new PackageDiffResult { Identical = identical, Added = added, Removed = removed, Changed = changed };
    }

    private static string HashItem(string path)
    {
        if (!path.StartsWith("zip:", StringComparison.Ordinal)) return HashFile(path);
        string[] parts = path[4..].Split('|', 2);
        using var archive = ZipFile.OpenRead(parts[0]);
        using var stream = archive.GetEntry(parts[1])?.Open() ?? throw new InvalidDataException("Thiếu mục ZIP: " + parts[1]);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
