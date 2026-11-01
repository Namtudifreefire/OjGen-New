// ZipService.cs - Tạo và kiểm tra file ZIP bằng System.IO.Compression (có sẵn trong .NET).
//
// Khác bản C++: không còn bộ nén Deflate tự viết; dùng thư viện chuẩn của .NET nên không giới hạn 4 GB / 65535 file.
using System.IO.Compression;

namespace OjGen.Core;

public sealed record ZipEntrySpec(string Name, string File);

public sealed class ZipStats
{
    public int Files;
    public ulong RawBytes;
    public ulong ZipBytes;
}

public static class ZipService
{
    /// <summary>Ghi ra file tạm rồi đổi tên, nên ZIP cũ chỉ bị thay khi ZIP mới hoàn tất.</summary>
    public static bool Create(string zipPath, IReadOnlyList<ZipEntrySpec> entries, bool compress, out ZipStats stats, out string? err)
    {
        stats = new ZipStats();
        err = null;
        string tmp = "";
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
            ArgumentNullException.ThrowIfNull(entries);
            string fullZipPath = Path.GetFullPath(zipPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullZipPath)!);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safeEntries = new List<(string Name, string File)>(entries.Count);
            foreach (var item in entries)
            {
                string name = NormalizeEntryName(item.Name);
                if (!names.Add(name)) throw new InvalidDataException("Tên file trùng nhau trong ZIP: " + name);
                if (!File.Exists(item.File)) throw new FileNotFoundException("Không tìm thấy file nguồn để đóng ZIP.", item.File);
                safeEntries.Add((name, item.File));
            }
            tmp = Path.Combine(Path.GetDirectoryName(fullZipPath)!, $".{Path.GetFileName(fullZipPath)}.{Guid.NewGuid():N}.tmp");
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
            {
                var level = compress ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
                foreach (var e in safeEntries)
                {
                    var ze = zip.CreateEntry(e.Name, level);
                    DateTime timestamp = File.GetLastWriteTimeUtc(e.File);
                    if (timestamp.Year < 1980) timestamp = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    else if (timestamp.Year > 2107) timestamp = new DateTime(2107, 12, 31, 23, 59, 58, DateTimeKind.Utc);
                    ze.LastWriteTime = new DateTimeOffset(timestamp);
                    using var src = new FileStream(e.File, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var dst = ze.Open();
                    src.CopyTo(dst);
                    stats.RawBytes += (ulong)src.Length;
                    stats.Files++;
                }
            }
            File.Move(tmp, fullZipPath, overwrite: true);
            stats.ZipBytes = (ulong)new FileInfo(fullZipPath).Length;
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            try { if (tmp.Length > 0 && File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
            return false;
        }
    }

    private static string NormalizeEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Tên file trong ZIP không được để trống.");
        string normalized = name.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains(':') || normalized.Any(char.IsControl) ||
            normalized.Split('/').Any(part => part.Length == 0 || part is "." or ".."))
            throw new InvalidDataException("Đường dẫn file trong ZIP không hợp lệ: " + name);
        return normalized;
    }

    /// <summary>Đọc lại ZIP: danh sách tên phải khớp đúng expectedNames; giải nén từng file đọc hết dữ liệu.</summary>
    public static bool Verify(string zipPath, IReadOnlyList<string> expectedNames, out string? err) =>
        VerifyCore(zipPath, expectedNames, null, out err);

    /// <summary>Như Verify nhưng so sánh từng byte nội dung với file nguồn.</summary>
    public static bool Verify(string zipPath, IReadOnlyList<ZipEntrySpec> entries, out string? err) =>
        VerifyCore(zipPath, entries.Select(e => NormalizeEntryName(e.Name)).ToList(), entries, out err);

    private static bool VerifyCore(string zipPath, IReadOnlyList<string> expectedNames, IReadOnlyList<ZipEntrySpec>? sources, out string? err)
    {
        err = null;
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var want = new HashSet<string>(expectedNames.Select(NormalizeEntryName), StringComparer.OrdinalIgnoreCase);
            if (zip.Entries.Count != want.Count)
            {
                err = $"số file trong ZIP ({zip.Entries.Count}) khác dự kiến ({want.Count})";
                return false;
            }
            var srcByName = sources?.ToDictionary(s => NormalizeEntryName(s.Name), s => s.File, StringComparer.OrdinalIgnoreCase);
            var buf = new byte[1 << 16];
            var buf2 = new byte[1 << 16];
            foreach (var e in zip.Entries)
            {
                if (!want.Remove(e.FullName))
                {
                    err = "ZIP chứa file không mong đợi: " + e.FullName;
                    return false;
                }
                using var s = e.Open();
                FileStream? cmp = null;
                try
                {
                    if (srcByName != null) cmp = new FileStream(srcByName[e.FullName], FileMode.Open, FileAccess.Read, FileShare.Read);
                    long total = 0;
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (cmp != null)
                        {
                            int got = 0;
                            while (got < n)
                            {
                                int r = cmp.Read(buf2, got, n - got);
                                if (r <= 0) { err = "nội dung khác file gốc: " + e.FullName; return false; }
                                got += r;
                            }
                            if (!buf.AsSpan(0, n).SequenceEqual(buf2.AsSpan(0, n)))
                            {
                                err = "nội dung khác file gốc: " + e.FullName;
                                return false;
                            }
                        }
                        total += n;
                    }
                    if (total != e.Length) { err = "sai dung lượng: " + e.FullName; return false; }
                    if (cmp != null && cmp.Length != total) { err = "nội dung khác file gốc: " + e.FullName; return false; }
                }
                finally { cmp?.Dispose(); }
            }
            if (want.Count != 0) { err = "ZIP thiếu file: " + want.First(); return false; }
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            return false;
        }
    }
}
