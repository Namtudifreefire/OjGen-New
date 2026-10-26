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
        string tmp = zipPath + ".tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
            {
                var level = compress ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
                foreach (var e in entries)
                {
                    var ze = zip.CreateEntry(e.Name, level);
                    ze.LastWriteTime = new DateTimeOffset(File.GetLastWriteTime(e.File));
                    using var src = new FileStream(e.File, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var dst = ze.Open();
                    src.CopyTo(dst);
                    stats.RawBytes += (ulong)src.Length;
                    stats.Files++;
                }
            }
            if (File.Exists(zipPath)) File.Delete(zipPath);
            File.Move(tmp, zipPath);
            stats.ZipBytes = (ulong)new FileInfo(zipPath).Length;
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
            return false;
        }
    }

    /// <summary>Đọc lại ZIP: danh sách tên phải khớp đúng expectedNames; giải nén từng file đọc hết dữ liệu.</summary>
    public static bool Verify(string zipPath, IReadOnlyList<string> expectedNames, out string? err) =>
        VerifyCore(zipPath, expectedNames, null, out err);

    /// <summary>Như Verify nhưng so sánh từng byte nội dung với file nguồn.</summary>
    public static bool Verify(string zipPath, IReadOnlyList<ZipEntrySpec> entries, out string? err) =>
        VerifyCore(zipPath, entries.Select(e => e.Name).ToList(), entries, out err);

    private static bool VerifyCore(string zipPath, IReadOnlyList<string> expectedNames, IReadOnlyList<ZipEntrySpec>? sources, out string? err)
    {
        err = null;
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var want = new HashSet<string>(expectedNames, StringComparer.Ordinal);
            if (zip.Entries.Count != want.Count)
            {
                err = $"số file trong ZIP ({zip.Entries.Count}) khác dự kiến ({want.Count})";
                return false;
            }
            var srcByName = sources?.ToDictionary(s => s.Name, s => s.File, StringComparer.Ordinal);
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
