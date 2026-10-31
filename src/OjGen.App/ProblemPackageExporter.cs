using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OjGen.Core;

namespace OjGen.Gui;

internal sealed class ProblemPackageOptions
{
    public string ParentDirectory { get; set; } = "";
    public string PackageName { get; set; } = "problem";
    public string TestDirectory { get; set; } = "";
    public bool IncludeTests { get; set; } = true;
    public bool CreateZip { get; set; } = true;
    public string ReferenceSolution { get; set; } = "";
    public bool IncludeReferenceSolution { get; set; }
    public string ValidatorPath { get; set; } = "";
    public string CheckerPath { get; set; } = "";
    public string CandidateSolution { get; set; } = "";
    public string AuditReport { get; set; } = "";
    public ulong? TestSeed { get; set; }
}

internal sealed class ProblemPackageResult
{
    public string Directory { get; set; } = "";
    public string ZipPath { get; set; } = "";
    public int InputCount { get; set; }
    public int OutputCount { get; set; }
    public int SampleCount { get; set; }
}

internal static class ProblemPackageExporter
{
    private const string PackageChecksumFile = "PACKAGE_SHA256SUMS.txt";

    private static string SafeName(string value)
    {
        string safe = new(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsControl(c) ? '_' : c).ToArray());
        safe = safe.Trim().Trim('.', ' ');
        return safe.Length == 0 ? "problem" : safe;
    }

    private static void CopyOptional(string source, string targetDirectory, string name)
    {
        if (source.Trim().Length == 0) return;
        if (!File.Exists(source)) throw new FileNotFoundException("Không tìm thấy file cần đưa vào gói.", source);
        Directory.CreateDirectory(targetDirectory);
        File.Copy(source, Path.Combine(targetDirectory, name + Path.GetExtension(source)), overwrite: false);
    }

    private static int WritePackageChecksums(string root)
    {
        string manifestPath = Path.Combine(root, PackageChecksumFile);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(manifestPath), StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var lines = new StringBuilder();
        foreach (string file in files)
        {
            using var stream = File.OpenRead(file);
            string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            string relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            lines.Append(hash).Append("  ").Append(relative).Append('\n');
        }
        File.WriteAllText(manifestPath, lines.ToString(), new UTF8Encoding(false));
        return files.Count;
    }

    private static string HashOptionalFile(string path)
    {
        if (path.Trim().Length == 0 || !File.Exists(path)) return "not recorded";
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void VerifyPackageZip(string packageDirectory, string zipPath)
    {
        var expected = Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(packageDirectory, path).Replace(Path.DirectorySeparatorChar, '/'),
                path => path, StringComparer.OrdinalIgnoreCase);
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Where(entry => !entry.FullName.EndsWith('/')).ToList();
        if (entries.Count != expected.Count) throw new InvalidDataException($"ZIP có {entries.Count} file nhưng gói có {expected.Count} file.");
        foreach (var entry in entries)
        {
            if (!expected.TryGetValue(entry.FullName, out string? source))
                throw new InvalidDataException("ZIP thiếu hoặc thừa file: " + entry.FullName);
            using var compressed = entry.Open();
            using var original = File.OpenRead(source);
            string zipHash = Convert.ToHexString(SHA256.HashData(compressed));
            string sourceHash = Convert.ToHexString(SHA256.HashData(original));
            if (!string.Equals(zipHash, sourceHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Nội dung trong ZIP không khớp: " + entry.FullName);
        }
    }

    public static ProblemPackageResult Export(ProblemPackageOptions options, ProblemAuthoringDocument document,
                                               Problem problem, Config config, IReadOnlyList<ManualTestSpec> manualTests)
    {
        if (options.ParentDirectory.Trim().Length == 0 || !Directory.Exists(options.ParentDirectory))
            throw new DirectoryNotFoundException("Chọn thư mục cha để tạo gói đề.");
        if (document.Name.Trim().Length == 0 || document.Code.Trim().Length == 0)
            throw new InvalidDataException("Bản đề cần có tên và mã bài trước khi đóng gói.");
        if (options.IncludeTests && !Directory.Exists(options.TestDirectory))
            throw new DirectoryNotFoundException("Thư mục test đã chọn không tồn tại.");
        if (options.IncludeReferenceSolution && options.ReferenceSolution.Trim().Length == 0)
            throw new InvalidDataException("Chọn file lời giải chuẩn hoặc bỏ chọn đưa lời giải vào gói.");
        List<string> testInputs = new();
        if (options.IncludeTests)
        {
            testInputs = FileManager.ListFilesWithExt(options.TestDirectory, ".inp");
            if (testInputs.Count == 0) throw new InvalidDataException("Không còn input để đưa vào gói.");
            foreach (string input in testInputs)
                if (!File.Exists(Path.ChangeExtension(input, ".out")))
                    throw new InvalidDataException($"Test {Path.GetFileName(input)} bị thiếu output sau khi kiểm định.");
            string? integrityError = FileManager.VerifyTestChecksumManifest(options.TestDirectory, out _);
            if (integrityError != null && integrityError != "Chưa có manifest SHA-256.")
                throw new InvalidDataException("Bộ test thay đổi sau khi kiểm định: " + integrityError);
        }

        string baseName = SafeName(options.PackageName.Length == 0 ? document.Code : options.PackageName);
        string parentDirectory = Path.GetFullPath(options.ParentDirectory);
        string packageDirectory;
        for (int suffix = 1; ; suffix++)
        {
            string candidateName = suffix == 1 ? baseName : baseName + "_" + suffix;
            packageDirectory = Path.Combine(parentDirectory, candidateName);
            string candidateZip = packageDirectory + ".zip";
            if (!Directory.Exists(packageDirectory) && !File.Exists(packageDirectory) &&
                (!options.CreateZip || (!File.Exists(candidateZip) && !Directory.Exists(candidateZip)))) break;
        }
        Directory.CreateDirectory(packageDirectory);

        File.WriteAllText(Path.Combine(packageDirectory, "statement.md"), document.ToMarkdown(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(packageDirectory, "statement.html"), document.ToHtml(), new UTF8Encoding(false));
        string packagedSolutionPath = options.IncludeReferenceSolution ? options.ReferenceSolution : "";
        ProblemProfileFile.From(problem, config, document.Name, document.Code, packagedSolutionPath).Save(Path.Combine(packageDirectory, "problem.ojproblem.json"));

        int inputs = 0, outputs = 0, samples = 0;
        if (options.IncludeTests)
        {
            var testDirectory = Path.Combine(packageDirectory, "tests");
            Directory.CreateDirectory(testDirectory);
            foreach (string input in testInputs)
            {
                File.Copy(input, Path.Combine(testDirectory, Path.GetFileName(input)), overwrite: false);
                inputs++;
                string output = Path.ChangeExtension(input, ".out");
                if (!File.Exists(output)) continue;
                File.Copy(output, Path.Combine(testDirectory, Path.GetFileName(output)), overwrite: false);
                outputs++;
            }
            string info = Path.Combine(options.TestDirectory, FileManager.InfoFile);
            if (File.Exists(info)) File.Copy(info, Path.Combine(packageDirectory, FileManager.InfoFile), overwrite: false);
            string sourceChecksums = Path.Combine(options.TestDirectory, FileManager.ChecksumFile);
            if (File.Exists(sourceChecksums))
                File.Copy(sourceChecksums, Path.Combine(testDirectory, FileManager.ChecksumFile), overwrite: false);
        }

        var samplesToWrite = manualTests.Select((test, index) => (test, index)).Where(item => item.test.IsSample).ToList();
        if (samplesToWrite.Count > 0)
        {
            string samplesDirectory = Path.Combine(packageDirectory, "samples");
            Directory.CreateDirectory(samplesDirectory);
            for (int i = 0; i < samplesToWrite.Count; i++)
            {
                var (sample, manualIndex) = samplesToWrite[i];
                string stem = "sample" + (i + 1).ToString("00");
                File.WriteAllText(Path.Combine(samplesDirectory, stem + ".inp"), sample.Input, new UTF8Encoding(false));
                string sampleOutput = sample.ExpectedOutput.Length > 0 ? sample.ExpectedOutput : sample.GeneratedOutput;
                if (sampleOutput.Length == 0 && manualIndex < testInputs.Count)
                {
                    string sourceOutput = Path.ChangeExtension(testInputs[manualIndex], ".out");
                    if (File.Exists(sourceOutput)) sampleOutput = FileManager.ReadText(sourceOutput);
                }
                if (sampleOutput.Length > 0)
                    File.WriteAllText(Path.Combine(samplesDirectory, stem + ".out"), sampleOutput, new UTF8Encoding(false));
                samples++;
            }
        }

        CopyOptional(options.IncludeReferenceSolution ? options.ReferenceSolution : "", Path.Combine(packageDirectory, "solutions"), "reference");
        CopyOptional(options.ValidatorPath, Path.Combine(packageDirectory, "tools"), "validator");
        CopyOptional(options.CheckerPath, Path.Combine(packageDirectory, "tools"), "checker");
        var manifest = new StringBuilder()
            .Append("Problem: ").Append(document.Name).Append(" [").Append(document.Code).Append("]\n")
            .Append("Input generator: ").Append(problem.Id).Append('\n')
            .Append("Time limit: ").Append(document.TimeLimitMs).Append(" ms\n")
            .Append("Memory limit: ").Append(document.MemoryLimitMb).Append(" MB\n")
            .Append("OjGen version: ").Append(Util.Version).Append('\n')
            .Append("Runtime: ").Append(RuntimeInformation.FrameworkDescription).Append('\n')
            .Append("OS: ").Append(RuntimeInformation.OSDescription).Append('\n')
            .Append("Test seed: ").Append(options.TestSeed.HasValue ? Util.LL(options.TestSeed.Value) : "not recorded").Append('\n')
            .Append("Reference solution file SHA-256: ").Append(HashOptionalFile(options.ReferenceSolution)).Append('\n')
            .Append("Validator file SHA-256: ").Append(HashOptionalFile(options.ValidatorPath)).Append('\n')
            .Append("Checker file SHA-256: ").Append(HashOptionalFile(options.CheckerPath)).Append('\n')
            .Append("Independent solution file SHA-256: ").Append(HashOptionalFile(options.CandidateSolution)).Append('\n')
            .Append("Tests: ").Append(inputs).Append(" input, ").Append(outputs).Append(" output\n")
            .Append("Samples: ").Append(samples).Append(" (sample output files are included when available)\n")
            .Append("Package checksum manifest excludes itself.\n");
        File.WriteAllText(Path.Combine(packageDirectory, "PACKAGE.txt"), manifest.ToString(), new UTF8Encoding(false));
        if (options.AuditReport.Length > 0)
            File.WriteAllText(Path.Combine(packageDirectory, "AUDIT_REPORT.txt"), options.AuditReport, new UTF8Encoding(false));
        int hashedFiles = WritePackageChecksums(packageDirectory);
        File.AppendAllText(Path.Combine(packageDirectory, "PACKAGE.txt"), $"Package checksums: {PackageChecksumFile} ({hashedFiles} files)\n", new UTF8Encoding(false));
        // Refresh the manifest after adding its description to PACKAGE.txt.
        hashedFiles = WritePackageChecksums(packageDirectory);

        string zipPath = "";
        if (options.CreateZip)
        {
            zipPath = packageDirectory + ".zip";
            try
            {
                ZipFile.CreateFromDirectory(packageDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                VerifyPackageZip(packageDirectory, zipPath);
            }
            catch
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch (Exception) { }
                throw;
            }
        }
        return new ProblemPackageResult { Directory = packageDirectory, ZipPath = zipPath, InputCount = inputs, OutputCount = outputs, SampleCount = samples };
    }
}
