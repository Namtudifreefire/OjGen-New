using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OjGen.Core;

namespace OjGen.Gui;

internal sealed class ProblemPackageOptions
{
    public string ExportFormat { get; set; } = "OjGen";
    public string ParentDirectory { get; set; } = "";
    public string PackageName { get; set; } = "problem";
    public string TestDirectory { get; set; } = "";
    public bool IncludeTests { get; set; } = true;
    public bool CreateZip { get; set; } = true;
    public string ReferenceSolution { get; set; } = "";
    public bool IncludeReferenceSolution { get; set; }
    public string ValidatorPath { get; set; } = "";
    public string CheckerPath { get; set; } = "";
    public string CheckerArguments { get; set; } = "{input} {output} {answer}";
    public int CheckerTimeoutMs { get; set; }
    public string ContractSuitePath { get; set; } = "";
    public string CandidateSolution { get; set; } = "";
    public string AuditReport { get; set; } = "";
    public ulong? TestSeed { get; set; }
    public JobSpec? GeneratedSpec { get; set; }
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

    private static string SafeIcpcPackageName(string value)
    {
        string name = new(value.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        return name.Length == 0 ? "problem" : name;
    }

    private static void CopyUtf8Text(string source, string target)
    {
        string content;
        try { content = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(source)); }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("Nội dung văn bản trong gói phải là UTF-8: " + Path.GetFileName(source), ex); }
        content = content.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (content.Length > 0 && !content.EndsWith('\n')) content += "\n";
        File.WriteAllText(target, content, new UTF8Encoding(false));
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

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static void WriteTestInventory(string root, IReadOnlyList<string> inputs, JobSpec? spec,
        IReadOnlyList<ManualTestSpec> manualTests)
    {
        var csv = new StringBuilder("name,role,group,tags,input_bytes,input_sha256,output_bytes,output_sha256\n");
        for (int i = 0; i < inputs.Count; i++)
        {
            string input = inputs[i];
            string output = Path.ChangeExtension(input, ".out");
            bool sample = i < manualTests.Count && manualTests[i].IsSample;
            string group = i < manualTests.Count ? manualTests[i].ConfigName : "";
            IReadOnlyList<string> tags = sample ? new[] { "sample" } : Array.Empty<string>();
            if (i >= manualTests.Count && spec?.Groups.Count > 0)
            {
                int generatedIndex = i - manualTests.Count;
                foreach (var item in spec.Groups)
                {
                    if (generatedIndex < item.TestCount) { group = item.Name; tags = item.CoverageTags; break; }
                    generatedIndex -= item.TestCount;
                }
            }
            long outputBytes = File.Exists(output) ? new FileInfo(output).Length : 0;
            string outputHash = File.Exists(output) ? HashOptionalFile(output) : "";
            csv.Append(Csv(Path.GetFileNameWithoutExtension(input))).Append(',')
                .Append(Csv(sample ? "sample" : "secret")).Append(',').Append(Csv(group)).Append(',')
                .Append(Csv(string.Join(';', tags))).Append(',').Append(new FileInfo(input).Length).Append(',')
                .Append(HashOptionalFile(input)).Append(',').Append(outputBytes).Append(',').Append(outputHash).Append('\n');
        }
        File.WriteAllText(Path.Combine(root, "TEST_INVENTORY.csv"), csv.ToString(), new UTF8Encoding(false));
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
        if (options.ExportFormat == "Icpc")
        {
            if (!options.IncludeTests) throw new InvalidDataException("Gói Kattis 2025-09 cần kèm bộ test bí mật.");
            if (!options.IncludeReferenceSolution || options.ReferenceSolution.Trim().Length == 0)
                throw new InvalidDataException("Gói Kattis 2025-09 cần lời giải chuẩn trong submissions/accepted.");
            if (!string.Equals(Path.GetExtension(options.ValidatorPath), ".py", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Gói Kattis 2025-09 cần validator Python (.py) để tạo launcher tương thích.");
            string solutionExtension = Path.GetExtension(options.ReferenceSolution);
            if (!new[] { ".cpp", ".cc", ".cxx", ".c++", ".c", ".py", ".java", ".cs" }
                    .Contains(solutionExtension, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Lời giải chuẩn trong gói Kattis phải là mã nguồn C, C++, Python, Java hoặc C#.");
        }

        string baseName = options.ExportFormat == "Icpc"
            ? SafeIcpcPackageName(options.PackageName.Length == 0 ? document.Code : options.PackageName)
            : SafeName(options.PackageName.Length == 0 ? document.Code : options.PackageName);
        string parentDirectory = Path.GetFullPath(options.ParentDirectory);
        string packageDirectory;
        for (int suffix = 1; ; suffix++)
        {
            string candidateName = suffix == 1 ? baseName : baseName + (options.ExportFormat == "Icpc" ? suffix.ToString() : "_" + suffix);
            packageDirectory = Path.Combine(parentDirectory, candidateName);
            string candidateZip = packageDirectory + ".zip";
            if (!Directory.Exists(packageDirectory) && !File.Exists(packageDirectory) &&
                (!options.CreateZip || (!File.Exists(candidateZip) && !Directory.Exists(candidateZip)))) break;
        }
        Directory.CreateDirectory(packageDirectory);

        try
        {
            if (options.ExportFormat != "Icpc" && options.GeneratedSpec is { Groups.Count: > 0 } generated)
                TestPlanProfile.From(generated.Problem, generated.Groups, generated.RequiredCoverageTags)
                    .Save(Path.Combine(packageDirectory, "TEST_PLAN.ojplan.json"));
            if (options.ExportFormat == "Icpc")
                return ExportIcpcPackage(options, document, problem, packageDirectory, testInputs, manualTests);

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

        WriteTestInventory(packageDirectory, testInputs, options.GeneratedSpec, manualTests);
        string provenanceSource = options.IncludeTests ? Path.Combine(options.TestDirectory, "ojgen_provenance.json") : "";
        if (provenanceSource.Length > 0 && File.Exists(provenanceSource)) File.Copy(provenanceSource, Path.Combine(packageDirectory, "ojgen_provenance.json"), overwrite: false);

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
            .Append("Checker argv template: ").Append(options.CheckerArguments).Append('\n')
            .Append("Checker timeout: ").Append(options.CheckerTimeoutMs > 0 ? options.CheckerTimeoutMs.ToString() : "use application timeout").Append(" ms\n")
            .Append("Contract suite SHA-256: ").Append(HashOptionalFile(options.ContractSuitePath)).Append('\n')
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

        var packageValidation = ProblemPackageValidator.Validate(packageDirectory, "OjGen");
        if (packageValidation.Errors.Count > 0)
            throw new InvalidDataException("Gói OjGen không qua kiểm tra cấu trúc:\n- " + string.Join("\n- ", packageValidation.Errors));

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
        catch
        {
            try { if (Directory.Exists(packageDirectory)) Directory.Delete(packageDirectory, recursive: true); } catch (Exception) { }
            try { if (File.Exists(packageDirectory + ".zip")) File.Delete(packageDirectory + ".zip"); } catch (Exception) { }
            throw;
        }
    }

    private static string StablePackageUuid(string code)
    {
        byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes("OjGen.ICPC:" + code.Trim().ToLowerInvariant()));
        byte[] bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes).ToString();
    }

    private static string YamlQuote(string value) => "\"" + new string(value.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray())
        .Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static ProblemPackageResult ExportIcpcPackage(ProblemPackageOptions options,
        ProblemAuthoringDocument document, Problem problem, string packageDirectory,
        IReadOnlyList<string> testInputs, IReadOnlyList<ManualTestSpec> manualTests)
    {
        string statementDir = Path.Combine(packageDirectory, "statement");
        string sampleDir = Path.Combine(packageDirectory, "data", "sample");
        string secretDir = Path.Combine(packageDirectory, "data", "secret");
        string solutionDir = Path.Combine(packageDirectory, "submissions", "accepted");
        string validatorDir = Path.Combine(packageDirectory, "input_validators");
        Directory.CreateDirectory(statementDir);
        Directory.CreateDirectory(sampleDir);
        Directory.CreateDirectory(secretDir);
        Directory.CreateDirectory(solutionDir);
        Directory.CreateDirectory(validatorDir);

        string markdown = document.ToMarkdown();
        int titleEnd = markdown.IndexOf('\n');
        if (titleEnd >= 0) markdown = markdown[(titleEnd + 1)..].TrimStart();
        File.WriteAllText(Path.Combine(statementDir, "problem.vi.md"), markdown, new UTF8Encoding(false));
        string timeSeconds = (document.TimeLimitMs / 1000d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        string yaml = "problem_format_version: \"2025-09\"\n" +
            "name:\n  vi: " + YamlQuote(document.Name) + "\n" +
            "uuid: " + StablePackageUuid(document.Code) + "\n" +
            "type: pass-fail\n" +
            "limits:\n  time_limit: " + timeSeconds + "\n  memory: " + document.MemoryLimitMb + "\n";
        File.WriteAllText(Path.Combine(packageDirectory, "problem.yaml"), yaml, new UTF8Encoding(false));

        string rawSolutionExt = Path.GetExtension(options.ReferenceSolution);
        string solutionExt = rawSolutionExt.Equals(".C", StringComparison.Ordinal) ? ".cpp" : rawSolutionExt.ToLowerInvariant();
        if (solutionExt is ".cc" or ".cxx" or ".c++") solutionExt = ".cpp";
        string solutionName = "reference" + solutionExt;
        CopyUtf8Text(options.ReferenceSolution, Path.Combine(solutionDir, solutionName));

        string validatorProgramDir = Path.Combine(validatorDir, "ojgen_validator");
        Directory.CreateDirectory(validatorProgramDir);
        CopyUtf8Text(options.ValidatorPath, Path.Combine(validatorProgramDir, "validator.py"));
        string launcher =
            "#!/bin/sh\n" +
            "set -eu\n" +
            "input=$(mktemp)\n" +
            "trap 'rm -f \"$input\"' EXIT HUP INT TERM\n" +
            "cat > \"$input\"\n" +
            "set +e\n" +
            "python3 \"$(dirname \"$0\")/validator.py\" \"$input\" \"$@\" < \"$input\"\n" +
            "status=$?\n" +
            "set -e\n" +
            "if [ \"$status\" -eq 0 ]; then exit 42; else exit \"$status\"; fi\n";
        string launcherPath = Path.Combine(validatorProgramDir, "run");
        File.WriteAllText(launcherPath, launcher, new UTF8Encoding(false));
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(launcherPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        int sampleCount = 0, secretCount = 0, outputCount = 0;
        int manualCount = Math.Min(manualTests.Count, testInputs.Count);
        for (int i = 0; i < testInputs.Count; i++)
        {
            string input = testInputs[i];
            string output = Path.ChangeExtension(input, ".out");
            if (!File.Exists(output)) throw new InvalidDataException($"Thiếu output cho {Path.GetFileName(input)}.");
            bool isSample = i < manualCount && manualTests[i].IsSample;
            if (isSample)
            {
                sampleCount++;
                string stem = "sample" + sampleCount.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                CopyUtf8Text(input, Path.Combine(sampleDir, stem + ".in"));
                CopyUtf8Text(output, Path.Combine(sampleDir, stem + ".ans"));
            }
            else
            {
                secretCount++;
                string stem = "test" + secretCount.ToString("00000", System.Globalization.CultureInfo.InvariantCulture);
                CopyUtf8Text(input, Path.Combine(secretDir, stem + ".in"));
                CopyUtf8Text(output, Path.Combine(secretDir, stem + ".ans"));
            }
            outputCount++;
        }
        if (secretCount == 0) throw new InvalidDataException("Gói ICPC/Kattis cần ít nhất một test trong data/secret; hãy giữ một test không đánh dấu Sample.");
        if (sampleCount == 0) Directory.Delete(sampleDir);
        File.WriteAllText(Path.Combine(validatorProgramDir, "README.txt"),
            "This package uses the Kattis Problem Package Format 2025-09 release candidate. Check support in the target judge before importing.\n" +
            "The run script wraps validator.py, reads the judge input from stdin, also supplies a temporary input path as argv[1], and maps validator exit code 0 to 42.\n" +
            "The Python validator must be standalone and must use only its input and command-line arguments.\n" +
            "If executable permissions were not preserved, run: chmod +x run\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(packageDirectory, "README-IMPORT.txt"),
            "This package uses the Kattis Problem Package Format 2025-09 release candidate. It is not widely supported; check the target judge before importing.\n" +
            "The validator launcher and accepted solution are source files. Review package metadata, statement, samples, and permissions before use.\n", new UTF8Encoding(false));

        WriteTestInventory(packageDirectory, testInputs, options.GeneratedSpec, manualTests);
        string provenanceSource = Path.Combine(options.TestDirectory, "ojgen_provenance.json");
        if (File.Exists(provenanceSource)) File.Copy(provenanceSource, Path.Combine(packageDirectory, "ojgen_provenance.json"), overwrite: false);
        if (options.AuditReport.Length > 0)
            File.WriteAllText(Path.Combine(packageDirectory, "AUDIT_REPORT.txt"), options.AuditReport, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(packageDirectory, "PACKAGE.txt"),
            $"Format: Kattis Problem Package 2025-09 (release candidate)\nProblem: {document.Name} [{document.Code}]\nOjGen version: {Util.Version}\nTests: {testInputs.Count}\nSamples: {manualTests.Count(test => test.IsSample)}\n",
            new UTF8Encoding(false));
        WritePackageChecksums(packageDirectory);
        var packageValidation = ProblemPackageValidator.Validate(packageDirectory, "Icpc");
        if (packageValidation.Errors.Count > 0)
            throw new InvalidDataException("Gói ICPC/Kattis không qua kiểm tra cấu trúc:\n- " + string.Join("\n- ", packageValidation.Errors));

        string zipPath = "";
        if (options.CreateZip)
        {
            zipPath = packageDirectory + ".zip";
            try
            {
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (string file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
                    {
                        string entryName = Path.GetRelativePath(packageDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
                        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        if (entryName == "input_validators/ojgen_validator/run") entry.ExternalAttributes = unchecked((int)(0x81ED << 16));
                        using var source = File.OpenRead(file);
                        using var target = entry.Open();
                        source.CopyTo(target);
                    }
                }
                VerifyPackageZip(packageDirectory, zipPath);
            }
            catch
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch (Exception) { }
                throw;
            }
        }
        return new ProblemPackageResult
        {
            Directory = packageDirectory, ZipPath = zipPath, InputCount = testInputs.Count,
            OutputCount = outputCount, SampleCount = sampleCount,
        };
    }
}
