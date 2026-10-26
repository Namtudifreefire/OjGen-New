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

    /// <summary>Kiểm tra cấu hình (miền từng tham số + logic của module). null nếu hợp lệ.</summary>
    public static string? ValidateFullConfig(Problem p, Config cfg) =>
        Problem.CheckParamRanges(p, cfg) ?? p.ValidateConfig(cfg);

    /// <summary>Seed ngẫu nhiên ngắn (tối đa 12 chữ số) cho dễ ghi lại.</summary>
    public static ulong RandomSeed() =>
        BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8), 0) % 1000000000000UL;

    /// <summary>
    /// Sinh TestCount file input. Mỗi file được module xác thực lại ngay sau khi sinh;
    /// sinh lại (tối đa vài lần) nếu trùng một test trước đó.
    /// </summary>
    public static GenerateResult GenerateInputs(GenerateRequest req, IJobLog log, CancellationToken ct = default)
    {
        var res = new GenerateResult();
        var p = req.Problem;
        if (req.TestCount < 1 || req.TestCount > MaxTests)
        {
            res.Error = $"Số lượng test phải trong [1, {MaxTests}].";
            return res;
        }
        if (ValidateFullConfig(p, req.Cfg) is { } ce)
        {
            res.Error = "Cấu hình không hợp lệ: " + ce;
            return res;
        }
        try { Directory.CreateDirectory(req.OutDir); }
        catch (Exception) { }
        if (!Directory.Exists(req.OutDir))
        {
            res.Error = "Không tạo được thư mục " + req.OutDir;
            return res;
        }

        var rng = new Rng(req.Seed);
        int width = FileManager.TestNumberWidth(req.TestCount);
        var seen = new Dictionary<ulong, int>();   // hash nội dung -> số thứ tự test
        int duplicates = 0;
        string dupList = "";
        for (int t = 1; t <= req.TestCount; t++)
        {
            if (ct.IsCancellationRequested) { res.Cancelled = true; res.Error = "Đã hủy theo yêu cầu."; return res; }
            string data = "";
            int sameAs = 0;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                data = p.Generate(rng, req.Cfg);
                if (!seen.TryGetValue(Util.Fnv1aAscii(data), out int prev)) { sameAs = 0; break; }
                sameAs = prev;
            }
            if (p.ValidateInput(data, req.Cfg) is { } ve)
            {
                // Không bao giờ xảy ra nếu module đúng; nếu có là lỗi của module.
                res.Error = $"Test {t} sinh ra không đúng định dạng/giới hạn (lỗi module '{p.Id}'): {ve}";
                return res;
            }
            if (sameAs != 0)
            {
                duplicates++;
                if (duplicates <= 10)
                    dupList += " " + FileManager.TestName(req.Prefix, t, width) + "=" + FileManager.TestName(req.Prefix, sameAs, width);
            }
            else
            {
                seen[Util.Fnv1aAscii(data)] = t;
            }
            string f = Path.Combine(req.OutDir, FileManager.TestName(req.Prefix, t, width) + ".inp");
            if (!FileManager.WriteFile(f, data, out string? werr)) { res.Error = werr ?? "Lỗi ghi file."; return res; }
            res.Inputs.Add(f);
            log.Progress("Sinh input", t, req.TestCount);
        }
        if (duplicates > 0)
            res.Warnings.Add($"{duplicates} test trùng với test trước đó vì miền dữ liệu quá nhỏ so với số test " +
                             $"({dupList.Trim()}{(duplicates > 10 ? " ..." : "")}). Hãy nới giới hạn hoặc giảm số test.");
        res.Ok = true;
        return res;
    }
}
