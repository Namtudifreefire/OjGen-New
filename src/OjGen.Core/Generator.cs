// Generator.cs - Sinh các file .inp cho một dạng bài.
using System.Security.Cryptography;

namespace OjGen.Core;

public sealed class GenerateRequest
{
    public Problem Problem { get; set; } = null!;
    public Config Cfg { get; set; } = new();
    public int TestCount { get; set; }
    public ulong Seed { get; set; }
    public string OutDir { get; set; } = "";
    public string Prefix { get; set; } = "test";
    public IReadOnlyList<TestGroupSpec> Groups { get; set; } = Array.Empty<TestGroupSpec>();
    public IReadOnlyList<ManualTestSpec> ManualTests { get; set; } = Array.Empty<ManualTestSpec>();
}

public sealed class GenerateResult
{
    public bool Ok { get; set; }
    public bool Cancelled { get; set; }
    public string Error { get; set; } = "";
    public List<string> Inputs { get; } = new();
    public List<string> Warnings { get; } = new();
}

public static class Generator
{
    public const int MaxTests = 10000;
    public const int MaxGroups = 100;

    /// <summary>Kiểm tra cấu hình (miền từng tham số + logic của module). null nếu hợp lệ.</summary>
    public static string? ValidateFullConfig(Problem p, Config cfg) =>
        Problem.CheckParamRanges(p, cfg) ?? p.ValidateConfig(cfg);

    /// <summary>Seed ngẫu nhiên ngắn (tối đa 12 chữ số) cho dễ ghi lại.</summary>
    public static ulong RandomSeed() =>
        BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8), 0) % 1000000000000UL;

    /// <summary>Seed riêng ổn định cho một nhóm; nhóm đầu giữ nguyên seed bộ test để tương thích.</summary>
    public static ulong GroupSeed(ulong baseSeed, int groupIndex, ulong? overrideSeed = null)
    {
        if (overrideSeed is { } own) return own;
        if (groupIndex <= 0) return baseSeed;
        unchecked
        {
            ulong z = baseSeed + 0x9E3779B97F4A7C15UL * (ulong)groupIndex;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>
    /// Sinh TestCount file input. Mỗi file được module xác thực lại ngay sau khi sinh;
    /// sinh lại (tối đa vài lần) nếu trùng một test trước đó.
    /// </summary>
    public static GenerateResult GenerateInputs(GenerateRequest req, IJobLog log, CancellationToken ct = default)
    {
        var res = new GenerateResult();
        var p = req.Problem;
        var groups = req.Groups.Count > 0
            ? req.Groups
            : new[] { new TestGroupSpec { Name = "Tất cả", TestCount = req.TestCount, Cfg = req.Cfg } };
        if (groups.Count > MaxGroups)
        {
            res.Error = $"Kế hoạch không được vượt quá {MaxGroups} nhóm.";
            return res;
        }
        if (req.ManualTests.Count > MaxTests)
        {
            res.Error = $"Không được vượt quá {MaxTests} test nhập tay.";
            return res;
        }
        var manualNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manual in req.ManualTests)
        {
            if (manual.Name.Trim().Length == 0 || !manualNames.Add(manual.Name.Trim()))
            {
                res.Error = "Tên các test nhập tay phải có nội dung và không được trùng nhau.";
                return res;
            }
            if (ValidateFullConfig(p, manual.Cfg) is { } manualConfigError)
            {
                res.Error = $"Test '{manual.Name}' có cấu hình không hợp lệ: {manualConfigError}";
                return res;
            }
            if (p.ValidateInput(manual.Input, manual.Cfg) is { } manualInputError)
            {
                res.Error = $"Test '{manual.Name}' không hợp lệ: {manualInputError}";
                return res;
            }
        }
        long totalLong = req.ManualTests.Count;
        var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            if (group.Name.Trim().Length == 0 || !groupNames.Add(group.Name.Trim()))
            {
                res.Error = "Tên các nhóm phải có nội dung và không được trùng nhau.";
                return res;
            }
            if (group.TestCount < 1 || group.TestCount > MaxTests)
            {
                res.Error = $"Số lượng test của nhóm '{group.Name}' phải trong [1, {MaxTests}].";
                return res;
            }
            totalLong += group.TestCount;
            if (totalLong > MaxTests)
            {
                res.Error = $"Tổng số test phải trong [1, {MaxTests}].";
                return res;
            }
            if (ValidateFullConfig(p, group.Cfg) is { } groupError)
            {
                res.Error = $"Cấu hình nhóm '{group.Name}' không hợp lệ: {groupError}";
                return res;
            }
        }
        int total = (int)totalLong;
        if (total < 1 || total > MaxTests)
        {
            res.Error = $"Số lượng test phải trong [1, {MaxTests}].";
            return res;
        }
        try { Directory.CreateDirectory(req.OutDir); }
        catch (Exception) { }
        if (!Directory.Exists(req.OutDir))
        {
            res.Error = "Không tạo được thư mục " + req.OutDir;
            return res;
        }

        int width = FileManager.TestNumberWidth(total);
        var seen = new Dictionary<ulong, int>();   // hash nội dung -> số thứ tự test
        int duplicates = 0;
        string dupList = "";
        int globalTest = 0;
        foreach (var manual in req.ManualTests)
        {
            if (ct.IsCancellationRequested) { res.Cancelled = true; res.Error = "Đã hủy theo yêu cầu."; return res; }
            globalTest++;
            ulong hash = Util.Fnv1aAscii(manual.Input);
            if (seen.TryGetValue(hash, out int previous))
            {
                duplicates++;
                if (duplicates <= 10)
                    dupList += " " + FileManager.TestName(req.Prefix, globalTest, width) + "=" + FileManager.TestName(req.Prefix, previous, width);
            }
            else seen[hash] = globalTest;
            string path = Path.Combine(req.OutDir, FileManager.TestName(req.Prefix, globalTest, width) + ".inp");
            if (!FileManager.WriteFile(path, manual.Input, out string? writeError)) { res.Error = writeError ?? "Lỗi ghi test nhập tay."; return res; }
            res.Inputs.Add(path);
            log.Progress("Ghi test nhập tay", globalTest, total);
        }
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            var rng = new Rng(GroupSeed(req.Seed, groupIndex, group.Seed));
            for (int localTest = 1; localTest <= group.TestCount; localTest++)
            {
                if (ct.IsCancellationRequested) { res.Cancelled = true; res.Error = "Đã hủy theo yêu cầu."; return res; }
                globalTest++;
                string data = "";
                int sameAs = 0;
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    data = p.Generate(rng, group.Cfg);
                    if (!seen.TryGetValue(Util.Fnv1aAscii(data), out int prev)) { sameAs = 0; break; }
                    sameAs = prev;
                }
                if (p.ValidateInput(data, group.Cfg) is { } ve)
                {
                    res.Error = $"Test {globalTest} (nhóm '{group.Name}', test {localTest}) sinh ra không đúng định dạng/giới hạn (lỗi module '{p.Id}'): {ve}";
                    return res;
                }
                if (sameAs != 0)
                {
                    duplicates++;
                    if (duplicates <= 10)
                        dupList += " " + FileManager.TestName(req.Prefix, globalTest, width) + "=" + FileManager.TestName(req.Prefix, sameAs, width);
                }
                else
                {
                    seen[Util.Fnv1aAscii(data)] = globalTest;
                }
                string f = Path.Combine(req.OutDir, FileManager.TestName(req.Prefix, globalTest, width) + ".inp");
                if (!FileManager.WriteFile(f, data, out string? werr)) { res.Error = werr ?? "Lỗi ghi file."; return res; }
                res.Inputs.Add(f);
                log.Progress($"Sinh input · {group.Name}", globalTest, total);
            }
        }
        if (duplicates > 0)
            res.Warnings.Add($"{duplicates} test trùng với test trước đó vì miền dữ liệu quá nhỏ so với số test " +
                             $"({dupList.Trim()}{(duplicates > 10 ? " ..." : "")}). Hãy nới giới hạn hoặc giảm số test.");
        res.Ok = true;
        return res;
    }
}
