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

public sealed record TestExportResult(string ArchivePath, string Format, int TestCount);

/// <summary>Import and content-address comparison helpers for test folders and OJ archives.</summary>
public static class TestPackageTools
{
    private const int MaxTests = 10000;
    private const int MaxScannedEntries = 50000;
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
            if (archive.Entries.Count > MaxScannedEntries)
                throw new InvalidDataException($"Gói test có quá {MaxScannedEntries} mục; dừng quét để tránh dùng quá nhiều tài nguyên.");
            int testFiles = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                string ext = Path.GetExtension(entry.FullName);
                if (!InputExtensions.Contains(ext) && !OutputExtensions.Contains(ext)) continue;
                if (++testFiles > MaxTests * 2)
                    throw new InvalidDataException($"Gói test vượt giới hạn {MaxTests} bộ input/output.");
                string key = CanonicalTestKey(entry.FullName, InputExtensions.Contains(ext));
                if (!result.TryAdd(key, "zip:" + path + "|" + entry.FullName))
                    throw new InvalidDataException("Có tên test trùng nhau trong gói: " + key);
            }
        }
        else if (Directory.Exists(path))
        {
            var pending = new Stack<string>();
            pending.Push(path);
            int scannedEntries = 0, testFiles = 0;
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                foreach (string entryPath in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++scannedEntries > MaxScannedEntries)
                        throw new InvalidDataException($"Thư mục có quá {MaxScannedEntries} mục; dừng quét để tránh dùng quá nhiều tài nguyên.");
                    FileAttributes attributes = File.GetAttributes(entryPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(entryPath);
                        continue;
                    }
                    string ext = Path.GetExtension(entryPath);
                    if (!InputExtensions.Contains(ext) && !OutputExtensions.Contains(ext)) continue;
                    if (++testFiles > MaxTests * 2)
                        throw new InvalidDataException($"Thư mục vượt giới hạn {MaxTests} bộ input/output.");
                    string key = CanonicalTestKey(Path.GetRelativePath(path, entryPath), InputExtensions.Contains(ext));
                    if (!result.TryAdd(key, entryPath)) throw new InvalidDataException("Có tên test trùng nhau sau khi rút gọn thư mục: " + key);
                }
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
        if (File.Exists(dest)) throw new IOException("Đường dẫn đích đang là một file.");
        if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any())
            throw new IOException("Thư mục đích phải mới hoặc đang rỗng; không ghi đè test hiện có.");
        string parent = Path.GetDirectoryName(dest)!;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, $".{Path.GetFileName(dest)}.ojgen-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        int width = FileManager.TestNumberWidth(inputs.Count), outCount = 0;
        var warnings = new List<string>();
        try
        {
            using var zip = File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ZipFile.OpenRead(source) : null;
            for (int inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
            {
                var input = inputs[inputIndex];
                int index = inputIndex + 1;
                string name = FileManager.TestName(prefix, index, width);
                CopyItem(input.Value, Path.Combine(staging, name + ".inp"), zip);
                string outKey = input.Key[..^4] + ".out";
                if (items.TryGetValue(outKey, out string? output))
                {
                    CopyItem(output, Path.Combine(staging, name + ".out"), zip);
                    outCount++;
                }
                else warnings.Add($"Test nguồn '{Stem(input.Key)}' chưa có output .out/.ans.");
            }
            if (!FileManager.WriteTestChecksumManifest(staging, out _, out string? checksumError))
                throw new IOException(checksumError ?? "Không tạo được manifest SHA-256.");

            string? emptyDestinationBackup = null;
            if (Directory.Exists(dest))
            {
                emptyDestinationBackup = dest + ".ojgen-empty-" + Guid.NewGuid().ToString("N");
                Directory.Move(dest, emptyDestinationBackup);
            }
            try { Directory.Move(staging, dest); }
            catch
            {
                if (emptyDestinationBackup != null && !Directory.Exists(dest))
                    Directory.Move(emptyDestinationBackup, dest);
                throw;
            }
            if (emptyDestinationBackup != null)
            {
                try { Directory.Delete(emptyDestinationBackup); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        catch
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
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

    /// <summary>
    /// Exports test data using common OJ directory/extension conventions. This is an interchange archive,
    /// not a full native problem package: statements, validators, scoring and judge-specific settings are omitted.
    /// </summary>
    public static TestExportResult ExportTestData(string sourceDirectory, string destinationZip, string format)
    {
        string source = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("Không tìm thấy thư mục test nguồn.");
        string destination = Path.GetFullPath(destinationZip);
        if (!destination.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("File xuất phải có phần mở rộng .zip.");
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("File ZIP đích đã tồn tại.");
        string normalized = format.Trim().ToLowerInvariant();
        if (normalized is not ("ojgen" or "polygon" or "testlib" or "domjudge" or "dmoj"))
            throw new InvalidDataException("Định dạng phải là ojgen, polygon, testlib, domjudge hoặc dmoj.");

        var items = ListTests(source);
        var inputs = items.Where(pair => pair.Key.EndsWith(".inp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToList();
        if (inputs.Count == 0) throw new InvalidDataException("Không tìm thấy input test trong thư mục nguồn.");
        foreach (var input in inputs)
            if (!items.ContainsKey(input.Key[..^4] + ".out")) throw new InvalidDataException("Thiếu output cho test " + input.Key[..^4] + ".");

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.ojgen-export-{Guid.NewGuid():N}.tmp");
        try
        {
            WriteExportArchive(temporary, normalized, inputs, items);
            File.Move(temporary, destination, overwrite: false);
        }
        catch
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        return new TestExportResult(destination, normalized, inputs.Count);
    }

    private static void WriteExportArchive(string path, string format,
        IReadOnlyList<KeyValuePair<string, string>> inputs, Dictionary<string, string> items)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using var yaml = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        if (format == "dmoj") yaml.WriteLine("test_cases:");
        int width = Math.Max(3, inputs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            string stem = (i + 1).ToString("D" + width, System.Globalization.CultureInfo.InvariantCulture);
            bool sample = input.Key.StartsWith("sample/", StringComparison.OrdinalIgnoreCase);
            string inputName, outputName;
            switch (format)
            {
                case "ojgen": inputName = "tests/test" + stem + ".inp"; outputName = "tests/test" + stem + ".out"; break;
                case "domjudge": string group = sample ? "sample" : "secret"; inputName = $"data/{group}/test{stem}.in"; outputName = $"data/{group}/test{stem}.ans"; break;
                case "dmoj": inputName = "testdata/test" + stem + ".in"; outputName = "testdata/test" + stem + ".out"; yaml.WriteLine($"- {{in: {inputName}, points: 1}}"); break;
                default: inputName = "tests/test" + stem + ".in"; outputName = "tests/test" + stem + ".ans"; break;
            }
            AddArchiveFile(archive, input.Value, inputName);
            AddArchiveFile(archive, items[input.Key[..^4] + ".out"], outputName);
        }
        if (format == "dmoj")
        {
            var entry = archive.CreateEntry("init.yml", CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false));
            writer.Write(yaml.ToString());
        }
        string note = format switch
        {
            "domjudge" => "Test-data-only interchange. Files use data/secret/*.in and *.ans. Add statement, problem.yaml, validator and accepted solution in the target judge's required format.",
            "dmoj" => "Test-data-only interchange. init.yml references input files and outputs use matching .out names. Add statement and judge-specific metadata before importing.",
            "polygon" or "testlib" => "Test-data-only interchange using tests/*.in and tests/*.ans. This is not a native Polygon export; review test names and configure generators, validators, checker and statements in Polygon.",
            _ => "OjGen test data archive (.inp/.out).",
        };
        var noteEntry = archive.CreateEntry("OJGEN-INTEROP.txt", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(noteEntry.Open(), new System.Text.UTF8Encoding(false))) writer.WriteLine(note);
    }

    private static void AddArchiveFile(ZipArchive archive, string source, string entryName)
    {
        string targetName = entryName.Replace('\\', '/');
        var entry = archive.CreateEntry(targetName, CompressionLevel.Optimal);
        using var input = File.OpenRead(source);
        using var output = entry.Open();
        input.CopyTo(output);
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
