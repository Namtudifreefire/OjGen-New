using System.Text;

namespace OjGen.Core;

/// <summary>Writes a complete sibling temporary file before replacing the destination.</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string contents, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(contents);
        WriteAllBytes(path, (encoding ?? new UTF8Encoding(false)).GetBytes(contents));
    }

    public static void WriteAllBytes(string path, byte[] contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        WriteAllBytes(path, contents.AsSpan());
    }

    public static void WriteAllBytes(string path, ReadOnlySpan<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 64 * 1024, FileOptions.None))
            {
                stream.Write(contents);
                stream.Flush();
            }

            // The temp file is on the same volume, so rename replaces the old file as one operation.
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
