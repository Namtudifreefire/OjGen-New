// FileManager.cs - Thao tác file/thư mục an toàn (UTF-8, không ghi đè ngoài ý muốn).
using System.Text;

namespace OjGen.Core;

public static class FileManager
{
    public const string InfoFile = "ojgen_info.txt";

    /// <summary>Ghi nhị phân (giữ nguyên \n, không đổi thành \r\n trên Windows).</summary>
    public static bool WriteFile(string path, string data, out string? err) =>
        WriteFile(path, Util.Utf8.GetBytes(data), out err);

    public static bool WriteFile(string path, byte[] data, out string? err)
    {
        err = null;
        try
        {
            File.WriteAllBytes(path, data);
            return true;
        }
        catch (DirectoryNotFoundException) { err = "Không tạo được file: " + path; }
        catch (IOException ex) { err = $"Lỗi khi ghi file (ổ đĩa đầy?): {path} ({ex.Message})"; }
        catch (UnauthorizedAccessException) { err = "Không có quyền ghi file: " + path; }
        return false;
    }

    public static bool WriteFile(string path, string data) => WriteFile(path, data, out _);

    public static bool ReadFile(string path, out byte[] data, out string? err)
    {
        err = null;
        data = Array.Empty<byte>();
        try
        {
            data = File.ReadAllBytes(path);
            return true;
        }
        catch (Exception) { err = "Không mở được file để đọc: " + path; }
        return false;
    }

    public static string ReadText(string path)
    {
        try { return Util.Utf8.GetString(File.ReadAllBytes(path)); }
        catch (Exception) { return ""; }
    }

    // ------------------------------------------------------------ tên test

    public static int TestNumberWidth(int testCount) => Math.Max(2, Util.LL(testCount).Length);

    public static string TestName(string prefix, int index, int width) =>
        prefix + Util.LL(index).PadLeft(width, '0');

    // ------------------------------------------------------------ liệt kê

    /// <summary>So sánh tự nhiên: tách phần số để test2 &lt; test10.</summary>
    internal static bool NaturalLess(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                int i2 = i, j2 = j;
                while (i2 < a.Length && char.IsAsciiDigit(a[i2])) i2++;
                while (j2 < b.Length && char.IsAsciiDigit(b[j2])) j2++;
                string na = a[i..i2].TrimStart('0'), nb = b[j..j2].TrimStart('0');
                if (na.Length != nb.Length) return na.Length < nb.Length;
                int cmp = string.CompareOrdinal(na, nb);
                if (cmp != 0) return cmp < 0;
                i = i2; j = j2;
            }
            else
            {
                if (a[i] != b[j]) return a[i] < b[j];
                i++; j++;
            }
        }
        return a.Length - i < b.Length - j;
    }

    /// <summary>Liệt kê file theo đuôi (vd ".inp", không phân biệt hoa thường), không đệ quy, sắp xếp tự nhiên.</summary>
    public static List<string> ListFilesWithExt(string dir, string ext)
    {
        var res = new List<string>();
        if (!Directory.Exists(dir)) return res;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir))
                if (string.Equals(Path.GetExtension(f), ext, StringComparison.OrdinalIgnoreCase)) res.Add(f);
        }
        catch (Exception) { }
        res.Sort((x, y) =>
        {
            string nx = Path.GetFileName(x), ny = Path.GetFileName(y);
            if (NaturalLess(nx, ny)) return -1;
            if (NaturalLess(ny, nx)) return 1;
            return 0;
        });
        return res;
    }

    public static int CountTestFiles(string dir) =>
        ListFilesWithExt(dir, ".inp").Count + ListFilesWithExt(dir, ".out").Count;

    /// <summary>Xóa CHỈ các file .inp, .out và file thông tin ojgen trong thư mục (không đệ quy). -1 nếu lỗi.</summary>
    public static long ClearTestFiles(string dir, out string? err)
    {
        err = null;
        var files = ListFilesWithExt(dir, ".inp");
        files.AddRange(ListFilesWithExt(dir, ".out"));
        string info = Path.Combine(dir, InfoFile);
        if (File.Exists(info)) files.Add(info);
        long removed = 0;
        foreach (var f in files)
        {
            try { File.Delete(f); }
            catch (Exception ex)
            {
                err = $"Không xóa được {f}: {ex.Message}";
                return -1;
            }
            removed++;
        }
        return removed;
    }

    /// <summary>Tìm tên thư mục chưa tồn tại (cả dạng .zip): base, base_2, base_3, ...</summary>
    public static string UniqueDir(string baseDir)
    {
        static bool Taken(string d) => Directory.Exists(d) || File.Exists(d) || File.Exists(d + ".zip");
        if (!Taken(baseDir)) return baseDir;
        for (int k = 2; ; k++)
        {
            string cand = baseDir + "_" + Util.LL(k);
            if (!Taken(cand)) return cand;
        }
    }
}
