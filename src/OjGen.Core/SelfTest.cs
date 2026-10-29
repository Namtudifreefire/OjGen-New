// SelfTest.cs - Tự kiểm tra: mọi module sinh dữ liệu hợp lệ, tái tạo được bằng seed,
// bộ xác thực bắt được input sai, cấu hình sai bị từ chối, ZIP tạo/đọc lại đúng.
// Chạy: OjGen.Cli --selftest   (hoặc nút "Kiểm tra nội bộ" trên giao diện)
using System.Text;

namespace OjGen.Core;

public static class SelfTest
{
    private sealed record Case(string Problem, (string, long)[] Ints, (string, string)[] Texts, int Tests, string Note);

    private static Case C(string problem, (string, long)[] ints, (string, string)[] texts, int tests, string note) =>
        new(problem, ints, texts, tests, note);

    private static readonly (string, long)[] NoInts = Array.Empty<(string, long)>();
    private static readonly (string, string)[] NoTexts = Array.Empty<(string, string)>();

    private static Config MakeConfig(Problem p, Case c)
    {
        var cfg = p.DefaultConfig();
        foreach (var (k, v) in c.Ints) cfg.Set(k, v);
        foreach (var (k, v) in c.Texts) cfg.SetText(k, v);
        return cfg;
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    /// <summary>Trả về 0 nếu mọi kiểm tra đạt, 1 nếu có thất bại.</summary>
    public static int Run(IJobLog log)
    {
        int pass = 0, fail = 0;
        void Check(bool ok, string what, string? detail = null)
        {
            if (ok) pass++;
            else
            {
                fail++;
                log.Line("  [FAIL] " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
            }
        }

        log.Line("ojgen --selftest: kiểm tra module, xác thực, seed và ZIP");
        const long MN = long.MinValue, MX = long.MaxValue;
        var valid = new List<Case>
        {
            C("single_n", NoInts, NoTexts, 50, "mặc định"),
            C("single_n", new[] { ("minVal", MN), ("maxVal", MX) }, NoTexts, 200, "toàn bộ phạm vi long long"),
            C("single_n", new[] { ("minVal", -5L), ("maxVal", -5L) }, NoTexts, 3, "miền một giá trị"),
            C("two_ints", NoInts, NoTexts, 50, "mặc định"),
            C("two_ints", new[] { ("minA", -10L), ("maxA", 10L), ("minB", 0L), ("maxB", 5L), ("order", 1L) }, NoTexts, 100, "a <= b"),
            C("array", NoInts, NoTexts, 50, "mặc định"),
            C("array", new[] { ("minN", 50L), ("maxN", 50L), ("minVal", 1L), ("maxVal", 50L), ("distinct", 1L) }, NoTexts, 20, "hoán vị 1..50"),
            C("array", new[] { ("minN", 1L), ("maxN", 300L), ("minVal", -1000000000000L), ("maxVal", -1L), ("sorted", 1L) }, NoTexts, 20, "âm, tăng dần"),
            C("array", new[] { ("minN", 5L), ("maxN", 100L), ("minVal", MN), ("maxVal", MX), ("distinct", 1L), ("sorted", 2L) }, NoTexts, 20, "khác nhau, giảm dần"),
            C("array", new[] { ("minN", 200000L), ("maxN", 200000L), ("minVal", 1L), ("maxVal", 1000000000L) }, NoTexts, 2, "n lớn"),
            C("queries", NoInts, NoTexts, 50, "mặc định"),
            C("queries", new[] { ("updatePct", 0L), ("minN", 1L), ("maxN", 1L) }, NoTexts, 20, "n = 1, chỉ hỏi đoạn"),
            C("queries", new[] { ("updatePct", 100L), ("minQ", 5L), ("maxQ", 20L) }, NoTexts, 20, "chỉ cập nhật"),
            C("prefix_sum_range", NoInts, NoTexts, 50, "khuôn sumarr mặc định"),
            C("prefix_sum_range", new[] { ("minN", 1L), ("maxN", 1L), ("minQ", 1L), ("maxQ", 1L), ("minVal", 0L), ("maxVal", 0L) }, NoTexts, 10, "n = 1, giá trị 0"),
            C("prefix_sum_range", new[] { ("minN", 1000L), ("maxN", 1000L), ("minQ", 1000L), ("maxQ", 1000L) }, NoTexts, 2, "mảng và truy vấn lớn"),
            C("prefix_subsequence_range", NoInts, NoTexts, 50, "khuôn QBOJ TDC mặc định"),
            C("prefix_subsequence_range", new[] { ("minN", 1L), ("maxN", 1L), ("minQ", 1L), ("maxQ", 1L), ("minVal", -10000000L), ("maxVal", -10000000L) }, NoTexts, 10, "n = q = 1, toàn số âm"),
            C("prefix_subsequence_range", new[] { ("minN", 100000L), ("maxN", 100000L), ("minQ", 100000L), ("maxQ", 100000L), ("minVal", -10000000L), ("maxVal", 10000000L) }, NoTexts, 1, "giới hạn TDC"),
            C("string", NoInts, NoTexts, 50, "mặc định"),
            C("string", new[] { ("printLen", 1L) }, new[] { ("charset", "digits") }, 30, "chữ số, ghi độ dài"),
            C("string", new[] { ("minLen", 1L), ("maxLen", 3L) }, new[] { ("charset", "abc") }, 30, "tập ký tự tự liệt kê"),
            C("string", new[] { ("minLen", 100000L), ("maxLen", 100000L) }, new[] { ("charset", "binary") }, 3, "xâu dài"),
            C("custom", NoInts, NoTexts, 100, "mặc định (100 test x 10 số)"),
            C("custom", new[] { ("n", 20L), ("min", -1000L), ("max", 1000L) }, NoTexts, 100, "100 test x 20 số, có số âm"),
            C("custom", new[] { ("n", 3L), ("mode", 2L) }, new[] { ("bounds", "1:10,50:100,-20:20") }, 50, "giới hạn riêng từng vị trí"),
            C("custom", new[] { ("n", 3L), ("mode", 2L) }, new[] { ("bounds", "1:10\n50:100\n-20:20") }, 20, "bounds mỗi vị trí một dòng"),
            C("custom", new[] { ("n", 2L), ("mode", 2L), ("header", 1L), ("layout", 2L) },
                new[] { ("bounds", "-9223372036854775808:9223372036854775807,-9:-1") }, 30, "giới hạn cực trị, mỗi số một dòng"),
            C("graph", NoInts, NoTexts, 50, "mặc định"),
            C("graph", new[] { ("minN", 10L), ("maxN", 10L), ("minM", 45L), ("maxM", 45L), ("connected", 1L) }, NoTexts, 5, "đồ thị đầy đủ K10"),
            C("graph", new[] { ("minN", 6L), ("maxN", 6L), ("minM", 30L), ("maxM", 30L), ("directed", 1L) }, NoTexts, 5, "có hướng đầy đủ"),
            C("graph", new[] { ("minN", 1L), ("maxN", 3L), ("minM", 0L), ("maxM", 20L), ("simple", 0L), ("weighted", 1L), ("minW", -5L), ("maxW", 5L) },
                NoTexts, 40, "đa đồ thị có khuyên"),
            C("graph", new[] { ("minN", 1L), ("maxN", 1L), ("minM", 0L), ("maxM", 0L) }, NoTexts, 1, "một đỉnh"),
            C("graph", new[] { ("minN", 2L), ("maxN", 50L), ("minM", 0L), ("maxM", 60L), ("connected", 1L), ("directed", 1L) }, NoTexts, 40, "có hướng liên thông yếu"),
            C("graph", new[] { ("minN", 200000L), ("maxN", 200000L), ("minM", 199999L), ("maxM", 300000L), ("connected", 1L), ("weighted", 1L) },
                NoTexts, 1, "đồ thị lớn"),
        };
        foreach (var c in valid)
        {
            var p = Problems.Find(c.Problem);
            Check(p != null, "tìm module " + c.Problem);
            if (p == null) continue;
            string tag = $"{c.Problem} ({c.Note})";
            var cfg = MakeConfig(p, c);
            var e = Generator.ValidateFullConfig(p, cfg);
            Check(e == null, tag + ": cấu hình hợp lệ", e);
            if (e != null) continue;
            var r1 = new Rng(20241025);
            var r2 = new Rng(20241025);
            bool allValid = true, same = true;
            string? firstErr = null;
            string first = "";
            for (int t = 0; t < c.Tests; t++)
            {
                string a = p.Generate(r1, cfg), b = p.Generate(r2, cfg);
                if (t == 0) first = a;
                if (a != b) same = false;
                var ve = p.ValidateInput(a, cfg);
                if (ve != null && allValid) { allValid = false; firstErr = ve; }
            }
            Check(allValid, $"{tag}: {c.Tests} test đều hợp lệ", firstErr);
            Check(same, tag + ": cùng seed cho cùng dữ liệu");
            // Bộ xác thực phải bắt được input bị hỏng.
            Check(p.ValidateInput(first[..^1], cfg) != null, tag + ": phát hiện thiếu xuống dòng cuối");
            Check(p.ValidateInput(first + "\n", cfg) != null, tag + ": phát hiện dòng trống thừa");
            Check(p.ValidateInput(first.Insert(first.Length - 1, " "), cfg) != null, tag + ": phát hiện khoảng trắng thừa");
        }
        log.Line($"  Đã kiểm tra {valid.Count} cấu hình hợp lệ.");

        // Kiểm tra giới hạn: giá trị ngoài miền phải bị bộ xác thực từ chối.
        {
            var p = Problems.Find("custom")!;
            var cfg = MakeConfig(p, C("custom", new[] { ("n", 3L), ("mode", 2L) }, new[] { ("bounds", "1:10,50:100,-20:20") }, 0, ""));
            Check(p.ValidateInput("10 100 -20\n", cfg) == null, "custom: biên đúng được chấp nhận");
            Check(p.ValidateInput("11 100 -20\n", cfg) != null, "custom: số thứ 1 vượt biên bị từ chối");
            Check(p.ValidateInput("1 49 0\n", cfg) != null, "custom: số thứ 2 dưới biên bị từ chối");
            Check(p.ValidateInput("1 50 21\n", cfg) != null, "custom: số thứ 3 vượt biên bị từ chối");
            Check(p.ValidateInput("1 50\n", cfg) != null, "custom: thiếu số bị từ chối");
            var g = Problems.Find("graph")!;
            var gcfg = MakeConfig(g, C("graph", new[] { ("minN", 3L), ("maxN", 3L), ("minM", 2L), ("maxM", 3L), ("connected", 1L) }, NoTexts, 0, ""));
            Check(g.ValidateInput("3 2\n1 2\n2 3\n", gcfg) == null, "graph: đồ thị đúng được chấp nhận");
            Check(g.ValidateInput("3 2\n1 2\n2 1\n", gcfg) != null, "graph: cạnh lặp bị từ chối");
            Check(g.ValidateInput("3 2\n1 1\n2 3\n", gcfg) != null, "graph: khuyên bị từ chối");
            Check(g.ValidateInput("3 2\n1 2\n2 4\n", gcfg) != null, "graph: đỉnh ngoài phạm vi bị từ chối");
            gcfg.Set("minM", 1);
            Check(g.ValidateInput("3 1\n1 2\n", gcfg) != null, "graph: không liên thông bị từ chối");
        }

        var invalid = new List<Case>
        {
            C("single_n", new[] { ("minVal", 5L), ("maxVal", 4L) }, NoTexts, 0, "min > max"),
            C("array", new[] { ("minN", 10L), ("maxN", 10L), ("minVal", 1L), ("maxVal", 5L), ("distinct", 1L) }, NoTexts, 0, "không đủ giá trị khác nhau"),
            C("array", new[] { ("minN", 0L) }, NoTexts, 0, "minN = 0 ngoài miền"),
            C("queries", new[] { ("minQ", 5L), ("maxQ", 4L) }, NoTexts, 0, "minQ > maxQ"),
            C("prefix_sum_range", new[] { ("minN", 5L), ("maxN", 4L) }, NoTexts, 0, "minN > maxN"),
            C("prefix_sum_range", new[] { ("minQ", 5L), ("maxQ", 4L) }, NoTexts, 0, "minQ > maxQ"),
            C("prefix_subsequence_range", new[] { ("minN", 5L), ("maxN", 4L) }, NoTexts, 0, "minN > maxN"),
            C("prefix_subsequence_range", new[] { ("minQ", 5L), ("maxQ", 4L) }, NoTexts, 0, "minQ > maxQ"),
            C("prefix_subsequence_range", new[] { ("minVal", 10000001L) }, NoTexts, 0, "giá trị vượt giới hạn TDC"),
            C("string", NoInts, new[] { ("charset", "a b") }, 0, "charset có dấu cách"),
            C("custom", new[] { ("n", 2L), ("mode", 2L) }, new[] { ("bounds", "1:2,3:4,5:6") }, 0, "bounds khác n"),
            C("custom", new[] { ("n", 1L), ("mode", 2L) }, new[] { ("bounds", "5:1") }, 0, "bounds lo > hi"),
            C("custom", new[] { ("n", 1L), ("mode", 2L) }, new[] { ("bounds", "1:99999999999999999999") }, 0, "bounds vượt long long"),
            C("custom", new[] { ("min", 10L), ("max", 1L) }, NoTexts, 0, "min > max"),
            C("graph", new[] { ("minN", 4L), ("maxN", 4L), ("minM", 7L), ("maxM", 7L) }, NoTexts, 0, "quá số cạnh đồ thị đơn"),
            C("graph", new[] { ("minN", 10L), ("maxN", 10L), ("minM", 3L), ("maxM", 3L), ("connected", 1L) }, NoTexts, 0, "không đủ cạnh để liên thông"),
            C("two_ints", new[] { ("minA", 10L), ("maxA", 20L), ("minB", 1L), ("maxB", 5L), ("order", 1L) }, NoTexts, 0, "không thể a <= b"),
        };
        foreach (var c in invalid)
        {
            var p = Problems.Find(c.Problem)!;
            Check(Generator.ValidateFullConfig(p, MakeConfig(p, c)) != null, $"{c.Problem}: từ chối cấu hình sai ({c.Note})");
        }
        log.Line($"  Đã kiểm tra {invalid.Count} cấu hình sai (phải bị từ chối).");

        // ConfigModel / JobPlanner (logic của form trên giao diện)
        {
            var m = new ConfigModel(Problems.Find("custom")!);
            Check(m.Validate() == null, "ConfigModel: cấu hình mặc định hợp lệ");
            m.EditInt("n", "abc");
            Check(m.FieldError("n") != null && m.Validate() != null, "ConfigModel: ô số nguyên sai bị báo lỗi");
            m.EditInt("n", "1e3");
            Check(m.FieldError("n") == null && m.Config.I("n") == 1000, "ConfigModel: nhận 1e3 = 1000");
            m.SetInt("mode", 2);
            Check(m.IsVisible(m.Spec("bounds")!) && !m.IsVisible(m.Spec("min")!), "ConfigModel: chế độ 2 hiện bounds, ẩn min");
            var changed = m.EditText("bounds", "1:10\n50:100\n-20:20");
            Check(m.Config.I("n") == 3 && changed.Contains("n"), "ConfigModel: dán bounds thì n tự bằng số vị trí");
            Check(m.Validate() == null, "ConfigModel: bounds + n khớp thì hợp lệ");
            m.SetInt("mode", 1);
            Check(m.Validate() == null && m.Effective().T("bounds") == "", "ConfigModel: bounds ẩn không gây lỗi ở chế độ 1");

            var st = new Settings { OutputRoot = "out_root" };
            var gm = new ConfigModel(Problems.Find("graph")!);
            var o = new JobPlanner.Options { TestsText = "5", SeedText = "42", SolutionPath = " \"sol.cpp\" " };
            string? err = JobPlanner.Build(gm, o, st, out var spec, out var pol);
            Check(err == null && spec != null && spec.TestCount == 5 && spec.Seed == 42UL && spec.SolutionPath == "sol.cpp"
                  && spec.OutDir == Path.Combine("out_root", "graph") && pol == ExistingPolicy.NewDir, "JobPlanner: dựng JobSpec đúng", err);
            Check(JobPlanner.Build(gm, new JobPlanner.Options { TestsText = "0" }, st, out _, out _) != null, "JobPlanner: từ chối số test = 0");
            Check(JobPlanner.Build(gm, new JobPlanner.Options { SeedText = "-1" }, st, out _, out _) != null, "JobPlanner: từ chối seed âm");
        }

        // RNG: kết quả chuẩn của MT19937-64 (seed mặc định 5489, giá trị thứ 10000 theo tài liệu C++11).
        {
            var r = new Rng(5489);
            ulong v = 0;
            for (int i = 0; i < 10000; i++) v = r.Next();
            Check(v == 9981545732273789042UL, "MT19937-64: giá trị thứ 10000 khớp chuẩn C++11", Util.LL(v));
        }

        // ZIP
        {
            var r = new Rng(7);
            var samples = new List<byte[]>
            {
                Array.Empty<byte>(),
                new[] { (byte)'a' },
                Encoding.ASCII.GetBytes("abcabcabcabcabcabcabc"),
                Enumerable.Repeat((byte)'x', 100000).ToArray(),
            };
            var rnd = new byte[300000];
            for (int i = 0; i < rnd.Length; i++) rnd[i] = (byte)r.Below(256);
            var txt = new StringBuilder();
            for (int i = 0; i < 50000; i++)
            {
                Util.AppendLL(txt, r.Range(-1000000, 1000000));
                txt.Append(i % 20 == 19 ? '\n' : ' ');
            }
            samples.Add(rnd);
            samples.Add(Encoding.ASCII.GetBytes(txt.ToString()));
            Check(Crc32(Encoding.ASCII.GetBytes("123456789")) == 0xCBF43926u, "CRC-32 chuẩn");

            string dir = Path.Combine(Path.GetTempPath(), "ojgen_selftest_" + Environment.ProcessId);
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                var entries = new List<ZipEntrySpec>();
                var names = new List<string>();
                for (int i = 0; i < samples.Count; i++)
                {
                    string nm = $"tệp_{i + 1}.inp";
                    File.WriteAllBytes(Path.Combine(dir, nm), samples[i]);
                    entries.Add(new ZipEntrySpec(nm, Path.Combine(dir, nm)));
                    names.Add(nm);
                }
                foreach (bool compress in new[] { true, false })
                {
                    string z = Path.Combine(dir, compress ? "nen.zip" : "luu.zip");
                    bool ok = ZipService.Create(z, entries, compress, out _, out string? err) && ZipService.Verify(z, entries, out err);
                    Check(ok, "tạo + kiểm tra ZIP " + (compress ? "(Deflate)" : "(stored)"), err);
                    var wrong = names.Take(names.Count - 1).ToList();
                    Check(!ZipService.Verify(z, wrong, out _), "Verify phát hiện thiếu/thừa file");
                    // Hỏng một byte dữ liệu -> phải bị phát hiện khi so với nguồn.
                    File.WriteAllBytes(Path.Combine(dir, "tệp_3.inp"), Encoding.ASCII.GetBytes("abcabcabcabcabcabcabX"));
                    Check(!ZipService.Verify(z, entries, out _), "Verify phát hiện nội dung khác file gốc");
                    File.WriteAllBytes(Path.Combine(dir, "tệp_3.inp"), samples[2]);
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }

        log.Line("");
        log.Line($"Kết quả: {pass} kiểm tra đạt, {fail} thất bại.");
        log.Line(fail == 0 ? "SELFTEST: ĐẠT" : "SELFTEST: KHÔNG ĐẠT");
        return fail == 0 ? 0 : 1;
    }
}
