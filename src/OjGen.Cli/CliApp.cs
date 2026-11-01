// CliApp.cs - Xử lý tham số dòng lệnh (chuyển từ cli.cpp).
using OjGen.Core;

namespace OjGen.Cli;

internal static class CliApp
{
    private sealed class CliProblemProfile
    {
        public int Version { get; set; } = 1;
        public string ProblemId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string SolutionPath { get; set; } = "";
        public Dictionary<string, long> Ints { get; set; } = new();
        public Dictionary<string, string> Texts { get; set; } = new();
    }

    private static string UsageText()
    {
        var s = new System.Text.StringBuilder();
        s.Append($"ojgen {Util.Version} - Công cụ sinh test cho bài lập trình thi đấu (bản dòng lệnh; giao diện: OjGen.App)\n\n");
        s.Append("CÁCH DÙNG\n");
        s.Append("  ojgen --list                       Liệt kê các dạng bài (module)\n");
        s.Append("  ojgen --info <dạng>                Xem định dạng input và tham số của một dạng bài\n");
        s.Append("  ojgen --topics [--topic-source S]  Liệt kê chủ đề thuật toán từ các OJ\n");
        s.Append("  ojgen --topic <nguồn:nhãn>         Xem bài mẫu và khuôn input được gợi ý\n");
        s.Append("  ojgen --problem <dạng> [tùy chọn]  Sinh bộ test\n");
        s.Append("  ojgen --regen-out <thư mục> --solution <file>   Tạo lại .out cho thư mục test có sẵn\n");
        s.Append("  ojgen --zip-dir <thư mục>          Đóng gói ZIP thư mục test có sẵn\n");
        s.Append("  ojgen --import-tests <thư mục|ZIP> --out <thư mục mới>  Nhập test OJ vào thư mục chuẩn\n");
        s.Append("  ojgen --compare-package <A> --against <B>  So sánh nội dung hai bộ test\n");
        s.Append("  ojgen --completion <bash|powershell>  Tạo gợi ý lệnh cho shell\n");
        s.Append("  ojgen --problem <dạng> --audit ...  Kiểm định phát hành, ghi báo cáo và chỉ ZIP khi đạt\n");
        s.Append("  ojgen --audit-dir <thư mục> --problem <dạng>  Kiểm định/đóng gói test có sẵn\n");
        s.Append("  ojgen --ci-project <file.ojproject> [--json-report report.json]  Kiểm định headless, không sửa thư mục nguồn\n");
        s.Append("  ojgen --preset-save <tên> --problem <dạng> [--plan FILE]  Lưu preset ra đề; --preset FILE để dùng\n");
        s.Append("  ojgen --preset-list [--preset-dir DIR]  Liệt kê preset đã lưu\n");
        s.Append("  ojgen --library DIR [--library-search QUERY] [--library-tag TAG]  Tìm/lọc đề theo nhãn nhóm test\n");
        s.Append("  ojgen --library-clone FILE --out FILE.ojproject  Nhân bản project để bắt đầu một đề mới\n");
        s.Append("  ojgen --contest contest.ojcontest.json  Kiểm tra workspace nhiều bài và đường dẫn project\n");
        s.Append("  ojgen --contest contest.ojcontest.json --contest-export contest.zip  Gộp package các bài thành một ZIP contest\n");
        s.Append("  ojgen --compare-problem A --against B  So sánh hai bản đề/project/thư mục\n");
        s.Append("  ojgen --check-resources statement.ojdraft.json  Tìm ảnh/link local bị hỏng hoặc không an toàn\n");
        s.Append("  ojgen --check-translations translations.ojtranslation.json  Kiểm tra bản dịch, ví dụ và giới hạn\n");
        s.Append("  ojgen --export-tests DIR --format <ojgen|polygon|domjudge|dmoj> --out FILE.zip  Xuất test-data interchange\n");
        s.Append("  ojgen --plugin-check DLL_OR_DIR  Kiểm tra plugin/API trước khi dùng (plugin được nạp trong process)\n");
        s.Append("  ojgen --selftest                   Tự kiểm tra các module và bộ tạo ZIP\n");
        s.Append("  ojgen --help | --version\n\n");
        s.Append("TÙY CHỌN KHI SINH TEST\n");
        s.Append($"  --tests N            Số lượng test (1..{Generator.MaxTests}, mặc định 10)\n");
        s.Append("  --seed S             Seed ngẫu nhiên (số nguyên không âm). Cùng seed + cùng cấu hình = cùng bộ test\n");
        s.Append("  --resume             Lưu checkpoint từ đầu hoặc tiếp tục; giữ nguyên seed và mọi tùy chọn cấu hình\n");
        s.Append("  --solution FILE      Lời giải (.c/.cpp, .py, .java, .kt, .pas/.pp, .cs, .rs, .go, .ojrun.json hoặc executable) tạo file .out\n");
        s.Append("  --out DIR            Thư mục chứa test (mặc định output/<dạng>)\n");
        s.Append("  --force              Ghi đè test cũ trong thư mục (mặc định: tự tạo thư mục mới <tên>_2, _3...)\n");
        s.Append("  --no-zip             Không tạo file ZIP (mặc định có tạo khi bộ test hoàn chỉnh hoặc chỉ có input)\n");
        s.Append("  --zip-incomplete     Vẫn tạo ZIP khi lời giải lỗi ở một số test\n");
        s.Append("  --rebuild            Biên dịch lại lời giải dù mã nguồn không đổi\n");
        s.Append("  --timeout MS         Thời gian tối đa mỗi test (ms)\n");
        s.Append("  --max-output-mb M    Dung lượng output tối đa mỗi test (MB)\n");
        s.Append("  --memory-mb M        Giới hạn bộ nhớ lời giải (MB, 0 = không giới hạn)\n");
        s.Append("  --compiler PATH      Trình biên dịch C++ (mặc định g++)\n");
        s.Append("  --cxxflags \"...\"     Cờ biên dịch\n");
        s.Append("  --python CMD         Lệnh chạy Python cho lời giải .py (mặc định tự tìm)\n");
        s.Append("  --prefix P           Tiền tố tên file test (mặc định test)\n");
        s.Append("  --no-compress        ZIP chỉ lưu, không nén\n");
        s.Append("  --settings FILE      File cấu hình (mặc định ojgen_settings.ini trong thư mục hiện tại)\n");
        s.Append("  --profile FILE       Dùng hồ sơ .ojproblem.json để nạp dạng bài và tham số mặc định\n");
        s.Append("  --plan FILE          Tái sử dụng kế hoạch nhóm .ojplan.json (chứa số lượng/cấu hình từng nhóm)\n");
        s.Append("  --coverage-required TAGS  Nhãn độ phủ bắt buộc, phân cách bằng dấu phẩy (vd small,boundary,stress)\n");
        s.Append("  --json-report FILE   Ghi kết quả cuối cùng dạng JSON vào file\n");
        s.Append("  --plugin PATH        Nạp plugin generator từ DLL hoặc thư mục plugins\n");
        s.Append("  --<tham số> GIÁ_TRỊ  Tham số riêng của dạng bài, xem bằng --info <dạng>\n\n");
        s.Append("KIỂM ĐỊNH PHÁT HÀNH (tùy chọn)\n");
        s.Append("  --audit               Kiểm định bộ vừa sinh trước khi tạo ZIP\n");
        s.Append("  --audit-dir DIR       Kiểm định thư mục test có sẵn; mặc định tạo ZIP nếu đạt\n");
        s.Append("  --statement FILE      Bản nháp .ojdraft.json từ giao diện để rà nội dung đề\n");
        s.Append("  --validator FILE      Chạy validator trên mọi input hợp lệ và một số input sai\n");
        s.Append("  --checker FILE        Chạy checker trên từng input/output trong audit\n");
        s.Append("  --checker-args TEXT   Mẫu argv: {input} {output} {answer} {test}\n");
        s.Append("  --checker-timeout MS  Timeout riêng mỗi lần chạy checker (mặc định theo --timeout)\n");
        s.Append("  --contract-suite FILE  Ca JSON validator/checker phải chấp nhận hoặc từ chối\n");
        s.Append("  --compare-solution FILE  Đối chiếu với --solution trên 100 test nhỏ\n");
        s.Append("  --allow-audit-warnings  Cho phép ZIP khi chỉ còn cảnh báo (lỗi vẫn chặn)\n");
        s.Append("  Khi --audit-dir, dùng --tests làm số input mong đợi; --seed giúp kiểm tra tái lập.\n");
        s.Append("  Báo cáo: AUDIT_REPORT.txt; ZIP kèm SHA256SUMS.txt, PACKAGE.txt và PACKAGE_SHA256SUMS.txt.\n\n");
        s.Append("CHỦ ĐỀ THUẬT TOÁN\n");
        s.Append("  --topics liệt kê chủ đề VNOI, QBOJ, Codeforces, LQDOJ, LTOJ và DMOJ; dùng --topic-source để lọc nguồn.\n");
        s.Append("  --topic chỉ hiển thị chi tiết nếu đứng một mình; khi kèm tùy chọn sinh test, OjGen dùng khuôn gợi ý\n");
        s.Append("  làm --problem mặc định. Chủ đề là gợi ý cấu trúc input, không thay cho việc đọc đề và giới hạn.\n\n");
        s.Append("VÍ DỤ\n");
        s.Append("  ojgen --list\n");
        s.Append("  ojgen --topics --topic-source LQDOJ\n");
        s.Append("  ojgen --topics --topic-source DMOJ\n");
        s.Append("  ojgen --topic \"LQDOJ:greedy (tham lam)\"\n");
        s.Append("  ojgen --topic \"DMOJ:Quy hoạch động\"\n");
        s.Append("  ojgen --topic \"LQDOJ:greedy (tham lam)\" --tests 20 --seed 2024\n");
        s.Append("  ojgen --problem array --tests 20 --seed 2024\n");
        s.Append("  ojgen --problem custom --tests 100 --seed 2024\n");
        s.Append("  ojgen --problem custom --tests 100 --n 20 --min -1000 --max 1000 --solution sol.cpp\n");
        s.Append("  ojgen --problem custom --tests 50 --bounds \"1:10,50:100,-20:20\" --solution \"D:\\OJ\\bai 1\\sol.cpp\"\n");
        s.Append("  ojgen --problem graph --tests 30 --minN 5 --maxN 1000 --minM 4 --maxM 5000 --connected 1 --weighted 1\n\n");
        s.Append("  ojgen --problem graph --tests 50 --seed 2026 --density 3 --directed 1 --connected 1  # mật độ dày\n\n");
        s.Append("  ojgen --problem array --tests 30 --seed 2024 --solution sol.cpp --audit --no-zip\n");
        s.Append("  ojgen --audit-dir output/array --problem array --tests 30 --seed 2024 --solution sol.cpp --allow-audit-warnings\n\n");
        s.Append("MÃ THOÁT: 0 = thành công, 1 = có khác biệt khi so sánh package hoặc lời giải lỗi/đã hủy, 2 = lỗi tham số, cấu hình hoặc kiểm định phát hành.\n");
        return s.ToString();
    }

    private static readonly string[] CompletionOptions =
    {
        "--help", "--version", "--list", "--topics", "--topic", "--topic-source", "--problem", "--info",
        "--tests", "--seed", "--solution", "--out", "--force", "--resume", "--no-zip", "--zip-incomplete",
        "--rebuild", "--timeout", "--max-output-mb", "--memory-mb", "--compiler", "--cxxflags", "--python",
        "--prefix", "--no-compress", "--settings", "--profile", "--plan", "--coverage-required", "--json-report",
        "--audit", "--audit-dir", "--validator", "--checker", "--checker-args", "--checker-timeout",
        "--compare-solution", "--statement", "--contract-suite", "--allow-audit-warnings", "--zip-dir", "--regen-out",
        "--import-tests", "--compare-package", "--against", "--completion", "--plugin", "--selftest",
        "--preset", "--preset-save", "--preset-list", "--preset-dir", "--ci-project", "--library", "--library-search",
        "--contest", "--compare-problem", "--check-resources", "--check-translations", "--format-info", "--export-tests", "--format", "--library-tag",
        "--library-clone",
        "--plugin-check",
        "--contest-export",
    };

    private static string CompletionText(string shell)
    {
        string options = string.Join(' ', CompletionOptions);
        return shell.ToLowerInvariant() switch
        {
            "bash" => $"_ojgen_complete() {{ local cur=\"${{COMP_WORDS[COMP_CWORD]}}\"; COMPREPLY=( $(compgen -W '{options}' -- \"$cur\") ); }}\ncomplete -F _ojgen_complete ojgen\n",
            "powershell" => "Register-ArgumentCompleter -CommandName ojgen -ScriptBlock { param($wordToComplete) " +
                string.Join(',', CompletionOptions.Select(option => "'" + option + "'")) +
                " | Where-Object { $_ -like \"$wordToComplete*\" } | ForEach-Object { [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_) } }\n",
            _ => "",
        };
    }

    private static bool WriteJsonReport(string path, object payload, out string? error)
    {
        error = null;
        try
        {
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            AtomicFile.WriteAllText(full, System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
            return true;
        }
        catch (Exception ex) { error = "Không ghi được JSON report: " + ex.Message; return false; }
    }

    private static int FinishWithJson(Args args, int exitCode, object payload)
    {
        if (!args.Kv.TryGetValue("json-report", out string? path)) return exitCode;
        if (WriteJsonReport(path, payload, out string? error)) return exitCode;
        Err(error ?? "Không ghi được JSON report.");
        return 2;
    }

    private static string ListText()
    {
        var s = new System.Text.StringBuilder("Các dạng bài có sẵn:\n");
        foreach (var group in Problems.All.GroupBy(p => p.MainCategory))
        {
            s.Append('\n').Append(group.Key).Append(":\n");
            foreach (var p in group)
                s.Append("  ").Append(p.Id).Append(' ', p.Id.Length < 10 ? 10 - p.Id.Length : 1).Append(p.Title).Append('\n');
        }
        s.Append("\nXem chi tiết: ojgen --info <dạng>\n");
        return s.ToString();
    }

    private static string TopicKey(AlgorithmTopic topic) => $"{topic.Source}:{topic.Name}";

    private static string ListTopicsText(string? sourceFilter)
    {
        var s = new System.Text.StringBuilder("Chủ đề thuật toán có sẵn:\n");
        var topics = AlgorithmTopics.All
            .Where(t => sourceFilter == null || string.Equals(t.Source, sourceFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var group in topics.GroupBy(t => t.Source))
        {
            s.Append('\n').Append(group.Key).Append(":\n");
            foreach (var topic in group)
            {
                s.Append("  ").Append(TopicKey(topic))
                    .Append(" | ").Append(topic.RecommendedProblemId);
                if (!string.IsNullOrWhiteSpace(topic.Category)) s.Append(" | ").Append(topic.Category);
                if (topic.ExampleProblem is { Length: > 0 } sample) s.Append(" | bài mẫu: ").Append(sample);
                s.Append('\n');
            }
        }
        s.Append("\nXem chi tiết: ojgen --topic \"<nguồn>:<nhãn>\"\n");
        return s.ToString();
    }

    private static bool TryResolveTopic(string query, out AlgorithmTopic? topic, out string error)
    {
        var all = AlgorithmTopics.All;
        var matches = all.Where(t =>
            string.Equals(TopicKey(t), query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.DisplayName, query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 0)
            matches = all.Where(t => string.Equals(t.Name, query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 1)
        {
            topic = matches[0];
            error = "";
            return true;
        }

        topic = null;
        if (matches.Count > 1)
        {
            error = $"Chủ đề '{query}' bị trùng giữa nhiều nguồn. Dùng một khóa đầy đủ:\n  " +
                string.Join("\n  ", matches.Select(TopicKey));
        }
        else
        {
            error = $"Không tìm thấy chủ đề '{query}'. Dùng ojgen --topics để xem danh mục.";
        }
        return false;
    }

    private static string TopicInfoText(AlgorithmTopic topic)
    {
        var s = new System.Text.StringBuilder();
        s.AppendLine(TopicKey(topic));
        if (!string.IsNullOrWhiteSpace(topic.Category)) s.AppendLine("Nhóm: " + topic.Category);
        var recommended = Problems.Find(topic.RecommendedProblemId);
        if (recommended == null)
        {
            s.AppendLine("Chưa có khuôn input tương ứng trong OjGen.");
            return s.ToString();
        }

        s.AppendLine($"Khuôn input gợi ý: --problem {recommended.Id} ({recommended.Title})");
        if (topic.ExampleProblemUrl is { } exampleUrl && topic.ExampleProblem is { } exampleProblem)
            s.AppendLine($"Bài mẫu: {exampleProblem} — {exampleUrl}");
        s.AppendLine($"Dùng để sinh test: ojgen --topic \"{TopicKey(topic)}\" --tests 20 --seed 2024");
        s.AppendLine();
        s.Append(InfoText(recommended));
        return s.ToString();
    }

    private static bool HasGenerationOptions(Args args) =>
        args.Kv.Keys.Any(k => k is not "topic" and not "settings") ||
        args.Flags.Any(f => f is not "help" and not "h" and not "version" and not "list" and not "topics" and not "selftest");

    private static string InfoText(Problem p)
    {
        var s = new System.Text.StringBuilder();
        s.Append(p.Id).Append(" - ").Append(p.Title)
            .Append("\nNhóm chính: ").Append(p.MainCategory)
            .Append("\n\nĐỊNH DẠNG INPUT\n");
        foreach (var line in p.FormatDescription.Split('\n'))
            if (line.Length > 0) s.Append("  ").Append(line).Append('\n');
        s.Append("\nTHAM SỐ (--tên giá_trị)\n");
        var d = p.DefaultConfig();
        foreach (var ps in p.Params)
        {
            s.Append("  --").Append(ps.Key).Append("  : ").Append(ps.Label);
            if (ps.Type == ParamType.Int)
                s.Append($" [mặc định {Util.LL(d.I(ps.Key))}, miền {Util.LL(ps.Lo)}..{Util.LL(ps.Hi)}]");
            else if (ps.Type == ParamType.Bool)
                s.Append($" [0/1, mặc định {Util.LL(d.I(ps.Key))}]");
            else
                s.Append($" [mặc định \"{d.T(ps.Key)}\"]");
            s.Append('\n');
            if (ps.Help.Length > 0) s.Append("        ").Append(ps.Help).Append('\n');
        }
        return s.ToString();
    }

    private sealed class Args
    {
        public Dictionary<string, string> Kv = new();
        public HashSet<string> Flags = new();
        public List<string> Order = new();   // các khóa tham số theo thứ tự xuất hiện
    }

    private static readonly HashSet<string> FlagOnly = new()
    {
        "help", "h", "list", "topics", "version", "selftest", "force", "no-zip", "zip", "zip-incomplete", "rebuild", "no-compress",
        "audit", "allow-audit-warnings", "resume", "preset-list",
    };

    private static readonly HashSet<string> GlobalKeys = new()
    {
        "problem", "tests", "seed", "solution", "out", "timeout", "max-output-mb", "memory-mb", "compiler", "cxxflags",
        "prefix", "info", "regen-out", "zip-dir", "python", "settings", "topic", "topic-source",
        "audit-dir", "validator", "checker", "checker-args", "checker-timeout", "compare-solution", "statement", "contract-suite", "profile", "plan", "coverage-required",
        "completion", "import-tests", "compare-package", "against", "json-report", "plugin",
        "preset", "preset-save", "preset-dir", "ci-project", "library", "library-search", "contest", "compare-problem",
        "check-resources", "check-translations", "format-info", "export-tests", "format",
        "library-clone", "library-tag",
        "plugin-check",
        "contest-export",
    };

    private static bool ParseArgs(IReadOnlyList<string> input, Args a, out string err)
    {
        err = "";
        for (int i = 0; i < input.Count; i++)
        {
            string s = input[i];
            if (s.StartsWith("--", StringComparison.Ordinal)) s = s[2..];
            else if (s == "-h" || s == "/?") s = "help";
            else
            {
                err = $"Đối số không hợp lệ: '{s}' (các tùy chọn bắt đầu bằng --).";
                return false;
            }
            string value = "";
            bool hasValue = false;
            int eq = s.IndexOf('=');
            if (eq >= 0)
            {
                value = s[(eq + 1)..];
                s = s[..eq];
                hasValue = true;
            }
            if (FlagOnly.Contains(s))
            {
                if (hasValue) { err = $"--{s} không nhận giá trị."; return false; }
                a.Flags.Add(s);
                continue;
            }
            if (!hasValue)
            {
                // Giá trị là đối số kế tiếp, kể cả khi bắt đầu bằng '-' (vd --min -20).
                if (i + 1 >= input.Count) { err = $"Thiếu giá trị cho --{s}."; return false; }
                value = input[++i];
            }
            if (a.Kv.ContainsKey(s)) { err = $"Tùy chọn --{s} bị lặp lại."; return false; }
            a.Kv[s] = value;
            a.Order.Add(s);
        }
        return true;
    }

    private static bool ApplySettingsOverrides(Args a, Settings st, out string err)
    {
        string error = "";
        bool Num(string k, long lo, long hi, out long v)
        {
            if (!Util.ParseLL(a.Kv[k], out v) || v < lo || v > hi)
            {
                error = $"--{k} phải là số nguyên trong [{lo}, {hi}].";
                return false;
            }
            return true;
        }
        bool ok = ApplyCore(a, st, Num, ref error);
        err = error;
        return ok;
    }

    private delegate bool NumFn(string k, long lo, long hi, out long v);

    private static bool ApplyCore(Args a, Settings st, NumFn Num, ref string err)
    {
        long x;
        if (a.Kv.ContainsKey("timeout")) { if (!Num("timeout", 1, 3600000, out x)) return false; st.TimeoutMs = (int)x; }
        if (a.Kv.ContainsKey("max-output-mb")) { if (!Num("max-output-mb", 1, 4096, out x)) return false; st.MaxOutputMB = x; }
        if (a.Kv.ContainsKey("memory-mb")) { if (!Num("memory-mb", 0, 1048576, out x)) return false; st.MemoryMB = x; }
        if (a.Kv.TryGetValue("compiler", out var comp)) st.Compiler = comp;
        if (a.Kv.TryGetValue("cxxflags", out var fl)) st.CxxFlags = fl;
        if (a.Kv.TryGetValue("python", out var py)) st.PythonCommand = py;
        if (a.Kv.TryGetValue("prefix", out var p))
        {
            foreach (char c in p)
                if (!(char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-'))
                {
                    err = "--prefix chỉ được chứa chữ, số, '_' và '-'.";
                    return false;
                }
            if (p.Length == 0) { err = "--prefix không được rỗng."; return false; }
            st.Prefix = p;
        }
        if (a.Flags.Contains("no-compress")) st.ZipCompress = false;
        return true;
    }

    private static void Err(string text) => Console.Error.WriteLine(text);

    private static int WorkspaceModes(Args args) => new[]
    {
        args.Kv.ContainsKey("preset-save"), args.Flags.Contains("preset-list"), args.Kv.ContainsKey("ci-project"),
        args.Kv.ContainsKey("library"), args.Kv.ContainsKey("contest"), args.Kv.ContainsKey("compare-problem"),
        args.Kv.ContainsKey("check-resources"), args.Kv.ContainsKey("check-translations"), args.Kv.ContainsKey("format-info"),
        args.Kv.ContainsKey("export-tests"), args.Kv.ContainsKey("library-clone"), args.Kv.ContainsKey("plugin-check"),
    }.Count(value => value);

    private static int RunWorkspaceCommand(Args a, IJobLog log)
    {
        try
        {
            if (a.Flags.Contains("preset-list"))
            {
                foreach (string path in AuthoringPresetStore.List(a.Kv.GetValueOrDefault("preset-dir")))
                {
                    try { var preset = AuthoringPreset.Load(path); Console.Out.WriteLine($"{preset.Name}\t{preset.ProblemId}\t{preset.TestCount} test\t{path}"); }
                    catch (Exception ex) { Console.Out.WriteLine($"INVALID\t{path}\t{ex.Message}"); }
                }
                return 0;
            }
            if (a.Kv.TryGetValue("preset-save", out string? presetName))
            {
                if (!a.Kv.TryGetValue("problem", out string? problemId)) throw new InvalidDataException("--preset-save cần --problem <dạng>.");
                var problem = Problems.Find(problemId) ?? throw new InvalidDataException("Không tìm thấy khuôn bài: " + problemId);
                TestPlanProfile plan = a.Kv.TryGetValue("plan", out string? planPath) ? TestPlanProfile.Load(planPath) : new TestPlanProfile { ProblemId = problem.Id };
                if (!string.Equals(plan.ProblemId, problem.Id, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ProblemId trong --plan không khớp --problem.");
                var requiredTags = plan.RequiredCoverageTags?.ToList() ?? new List<string>();
                if (a.Kv.TryGetValue("coverage-required", out string? requiredTagText))
                {
                    if (TestCoverageTags.Parse(requiredTagText, out var parsedTags) is { } tagError) throw new InvalidDataException(tagError);
                    requiredTags = requiredTags.Concat(parsedTags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    plan.RequiredCoverageTags = requiredTags.ToList();
                }
                Config config = problem.DefaultConfig();
                foreach (var parameter in problem.Params)
                {
                    if (!a.Kv.TryGetValue(parameter.Key, out string? raw)) continue;
                    if (parameter.Type == ParamType.Text) config.SetText(parameter.Key, raw);
                    else if (Util.ParseLL(raw, out long number)) config.Set(parameter.Key, number);
                    else throw new InvalidDataException($"Tham số --{parameter.Key} không phải số nguyên hợp lệ.");
                }
                if (Generator.ValidateFullConfig(problem, config) is { } configError) throw new InvalidDataException(configError);
                ulong? seed = null;
                if (a.Kv.TryGetValue("seed", out string? seedText))
                {
                    if (!Util.ParseULL(seedText, out ulong parsed)) throw new InvalidDataException("--seed phải là số nguyên không âm.");
                    seed = parsed;
                }
                int count = 10;
                if (a.Kv.TryGetValue("tests", out string? countText) && (!int.TryParse(countText, out count) || count < 1 || count > Generator.MaxTests))
                    throw new InvalidDataException($"--tests phải trong [1, {Generator.MaxTests}].");
                var preset = new AuthoringPreset
                {
                    Name = presetName, ProblemId = problem.Id, TestCount = plan.Groups.Count > 0 ? plan.TotalTestCount : count,
                    ReferenceSolution = a.Kv.GetValueOrDefault("solution", ""),
                    Seed = seed, SeedPolicy = seed.HasValue ? "Fixed" : "Random", TestPlan = plan,
                    ValidatorPath = a.Kv.GetValueOrDefault("validator", ""), CheckerPath = a.Kv.GetValueOrDefault("checker", ""),
                    CheckerArguments = a.Kv.GetValueOrDefault("checker-args", "{input} {output} {answer}"),
                    CheckerTimeoutMs = a.Kv.TryGetValue("checker-timeout", out string? timeoutText) && int.TryParse(timeoutText, out int timeout) ? timeout : 0,
                    ContractSuitePath = a.Kv.GetValueOrDefault("contract-suite", ""),
                    RequiredCoverageTags = requiredTags,
                    Ints = new(config.Ints), Texts = new(config.Texts),
                };
                string saved = AuthoringPresetStore.Save(preset, a.Kv.GetValueOrDefault("preset-dir"));
                Console.Out.WriteLine("Đã lưu preset: " + saved);
                return 0;
            }
            if (a.Kv.TryGetValue("ci-project", out string? ciProject))
            {
                var outcome = CliProjectCiAudit.Run(ciProject, log);
                if (outcome.Error.Length > 0) Err("LỖI CI: " + outcome.Error);
                else Console.Out.WriteLine(outcome.Text);
                if (a.Kv.TryGetValue("json-report", out string? reportPath) && !WriteJsonReport(reportPath, outcome.Json, out string? jsonError))
                { Err(jsonError ?? "Không ghi được JSON report."); return 2; }
                return outcome.ExitCode;
            }
            if (a.Kv.TryGetValue("library", out string? libraryPath))
            {
                var entries = ProblemLibrary.Scan(libraryPath, a.Kv.GetValueOrDefault("library-search"), a.Kv.GetValueOrDefault("library-tag"));
                if (a.Kv.TryGetValue("json-report", out string? reportPath) && !WriteJsonReport(reportPath, new { count = entries.Count, entries }, out string? jsonError))
                    throw new IOException(jsonError ?? "Không ghi được JSON report.");
                foreach (var entry in entries) Console.Out.WriteLine($"{entry.Code}\t{entry.Name}\t{entry.ProblemId}\t[{string.Join(',', entry.Tags)}]\t{entry.Kind}\t{entry.Path}");
                Console.Out.WriteLine($"Tổng: {entries.Count} mục.");
                return 0;
            }
            if (a.Kv.TryGetValue("library-clone", out string? cloneSource))
            {
                if (!a.Kv.TryGetValue("out", out string? cloneDestination)) throw new InvalidDataException("--library-clone cần --out <file.ojproject>.");
                string cloned = ProblemLibrary.CloneProject(cloneSource, cloneDestination);
                Console.Out.WriteLine("Đã nhân bản project: " + cloned);
                return 0;
            }
            if (a.Kv.TryGetValue("plugin-check", out string? pluginCheckPath))
            {
                if (a.Kv.ContainsKey("plugin")) throw new InvalidDataException("Dùng --plugin-check PATH riêng; không kèm --plugin.");
                var messages = ProblemPluginLoader.LoadFrom(pluginCheckPath);
                foreach (string message in messages) Console.Out.WriteLine(message);
                bool failed = messages.Count == 0 || messages.Any(message => message.StartsWith("Không nạp", StringComparison.OrdinalIgnoreCase) ||
                    message.StartsWith("Không tìm thấy plugin", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("ID dạng bài rỗng hoặc trùng", StringComparison.OrdinalIgnoreCase));
                Console.Out.WriteLine(failed ? "Plugin chưa đạt kiểm tra tương thích." : "Plugin đã được nạp và tương thích API.");
                return failed ? 2 : 0;
            }
            if (a.Kv.TryGetValue("contest", out string? contestPath))
            {
                var contest = ContestWorkspaceDocument.Load(contestPath);
                string root = Path.GetDirectoryName(Path.GetFullPath(contestPath))!;
                var errors = new List<string>();
                var warnings = new List<string>();
                foreach (var item in contest.Problems.OrderBy(item => item.Order))
                {
                    string projectPath = Path.GetFullPath(Path.IsPathRooted(item.ProjectPath) ? item.ProjectPath : Path.Combine(root, item.ProjectPath));
                    if (item.PackagePath.Length > 0) item.PackagePath = Path.GetFullPath(Path.IsPathRooted(item.PackagePath) ? item.PackagePath : Path.Combine(root, item.PackagePath));
                    if (!File.Exists(projectPath)) { errors.Add($"{item.Code}: thiếu project {projectPath}"); continue; }
                    try
                    {
                        using var project = System.Text.Json.JsonDocument.Parse(File.ReadAllText(projectPath));
                        string id = project.RootElement.TryGetProperty("ProblemId", out var idValue) ? idValue.GetString() ?? "" : "";
                        string code = project.RootElement.TryGetProperty("ProblemCode", out var codeValue) ? codeValue.GetString() ?? "" : "";
                        if (id.Length == 0 || Problems.Find(id) == null) errors.Add($"{item.Code}: project dùng ProblemId không hợp lệ.");
                        if (code.Length > 0 && !string.Equals(code, item.Code, StringComparison.OrdinalIgnoreCase)) warnings.Add($"{item.Code}: mã trong project là '{code}'.");
                    }
                    catch (Exception ex) { errors.Add($"{item.Code}: project không đọc được: {ex.Message}"); }
                }
                Console.Out.WriteLine($"Contest '{contest.Name}': {contest.Problems.Count} bài, {contest.DurationMinutes} phút.");
                foreach (string error in errors) Console.Out.WriteLine("LỖI: " + error);
                foreach (string warning in warnings) Console.Out.WriteLine("LƯU Ý: " + warning);
                string? archivePath = null;
                if (errors.Count == 0 && a.Kv.TryGetValue("contest-export", out string? contestExport))
                {
                    archivePath = contest.ExportArchive(contestExport);
                    Console.Out.WriteLine("Đã gộp package contest: " + archivePath);
                }
                var result = new { passed = errors.Count == 0, contest.Name, contest.DurationMinutes, problemCount = contest.Problems.Count, archivePath, errors, warnings };
                if (a.Kv.TryGetValue("json-report", out string? reportPath) && !WriteJsonReport(reportPath, result, out string? jsonError))
                    throw new IOException(jsonError ?? "Không ghi được JSON report.");
                return errors.Count > 0 ? 2 : warnings.Count > 0 ? 1 : 0;
            }
            if (a.Kv.TryGetValue("compare-problem", out string? left))
            {
                if (!a.Kv.TryGetValue("against", out string? right)) throw new InvalidDataException("--compare-problem cần --against <đường dẫn B>.");
                var diff = ProblemVersionComparer.Compare(left, right);
                foreach (string item in diff.Added) Console.Out.WriteLine("+ " + item);
                foreach (string item in diff.Removed) Console.Out.WriteLine("- " + item);
                foreach (string item in diff.Changed) Console.Out.WriteLine("* " + item);
                foreach (string item in diff.ReproducibilityRisks) Console.Out.WriteLine("! Rủi ro tái lập: " + item);
                Console.Out.WriteLine(diff.Same ? "Hai phiên bản giống nhau." : $"Khác nhau: +{diff.Added.Count} -{diff.Removed.Count} *{diff.Changed.Count}.");
                if (a.Kv.TryGetValue("json-report", out string? reportPath) && !WriteJsonReport(reportPath,
                    new { same = diff.Same, diff.Added, diff.Removed, diff.Changed, diff.ReproducibilityRisks }, out string? jsonError))
                    throw new IOException(jsonError ?? "Không ghi được JSON report.");
                return diff.Same ? 0 : 1;
            }
            if (a.Kv.TryGetValue("check-resources", out string? statementPath))
            {
                string source;
                if (Path.GetFileName(statementPath).EndsWith(".ojdraft.json", StringComparison.OrdinalIgnoreCase))
                {
                    using var statement = System.Text.Json.JsonDocument.Parse(File.ReadAllText(statementPath));
                    source = string.Join("\n", "# " + ReadJsonString(statement.RootElement, "Name"), ReadJsonString(statement.RootElement, "Description"),
                        ReadJsonString(statement.RootElement, "Input"), ReadJsonString(statement.RootElement, "Output"), ReadJsonString(statement.RootElement, "Constraints"), ReadJsonString(statement.RootElement, "Examples"));
                }
                else source = File.ReadAllText(statementPath);
                var resources = StatementResourceManager.CheckContent(source, Path.GetDirectoryName(Path.GetFullPath(statementPath))!);
                foreach (string item in resources.Missing) Console.Out.WriteLine("MISSING: " + item);
                foreach (string item in resources.Unsafe) Console.Out.WriteLine("UNSAFE: " + item);
                foreach (string item in resources.Remote) Console.Out.WriteLine("REMOTE (offline, not checked): " + item);
                Console.Out.WriteLine(resources.IsValid ? "Tài nguyên nội bộ hợp lệ." : "Có tài nguyên bị thiếu hoặc đường dẫn không an toàn.");
                if (a.Kv.TryGetValue("json-report", out string? reportPath) && !WriteJsonReport(reportPath,
                    new { passed = resources.IsValid, resources.Missing, resources.Unsafe, resources.Remote }, out string? jsonError)) throw new IOException(jsonError ?? "Không ghi được JSON report.");
                return resources.IsValid ? 0 : 2;
            }
            if (a.Kv.TryGetValue("check-translations", out string? translationsPath))
            {
                var report = TranslationManager.Check(translationsPath);
                foreach (string item in report.Errors) Console.Out.WriteLine("LỖI: " + item);
                foreach (string item in report.Warnings) Console.Out.WriteLine("LƯU Ý: " + item);
                Console.Out.WriteLine(report.Passed ? "Kiểm tra bản dịch đạt." : "Kiểm tra bản dịch chưa đạt.");
                if (a.Kv.TryGetValue("json-report", out string? reportPath) && !WriteJsonReport(reportPath,
                    new { passed = report.Passed, report.Errors, report.Warnings }, out string? jsonError)) throw new IOException(jsonError ?? "Không ghi được JSON report.");
                return report.Passed ? report.Warnings.Count > 0 ? 1 : 0 : 2;
            }
            if (a.Kv.TryGetValue("format-info", out string? format))
            {
                Console.Out.WriteLine("Định dạng test-data có thể nhập: " + string.Join("; ", ProblemFormatCompatibility.SupportedTestFormats));
                Console.Out.WriteLine(ProblemFormatCompatibility.CompatibilityNote(format));
                return 0;
            }
            if (a.Kv.TryGetValue("export-tests", out string? exportSource))
            {
                if (!a.Kv.TryGetValue("format", out string? formatName) || !a.Kv.TryGetValue("out", out string? outputPath))
                    throw new InvalidDataException("--export-tests cần --format <định dạng> và --out <file.zip>.");
                var exported = TestPackageTools.ExportTestData(exportSource, outputPath, formatName);
                Console.Out.WriteLine($"Đã xuất {exported.TestCount} test-data dạng {exported.Format}: {exported.ArchivePath}");
                Console.Out.WriteLine("Đây là gói test-data trung gian, không gồm statement, checker, validator hoặc toàn bộ metadata native của judge.");
                if (a.Kv.TryGetValue("json-report", out string? reportPath) && !WriteJsonReport(reportPath,
                    new { archivePath = exported.ArchivePath, exported.Format, exported.TestCount }, out string? jsonError)) throw new IOException(jsonError ?? "Không ghi được JSON report.");
                return 0;
            }
        }
        catch (Exception ex) { Err("LỖI workspace: " + ex.Message); return 2; }
        return 2;
    }

    private static string ReadJsonString(System.Text.Json.JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() ?? "" : "";

    public static int Run(string[] argv)
    {
        var a = new Args();
        if (!ParseArgs(argv, a, out string err))
        {
            Err("LỖI: " + err + "\nXem hướng dẫn: ojgen --help");
            return 2;
        }
        var log = new ConsoleLog();
        string defaultPluginPath = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!a.Kv.ContainsKey("plugin-check"))
        {
            if (a.Kv.TryGetValue("plugin", out string? pluginPath))
                foreach (string pluginMessage in ProblemPluginLoader.LoadFrom(pluginPath))
                    Console.Error.WriteLine("PLUGIN: " + pluginMessage);
            else if (Directory.Exists(defaultPluginPath))
                foreach (string pluginMessage in ProblemPluginLoader.LoadFrom(defaultPluginPath))
                    Console.Error.WriteLine("PLUGIN: " + pluginMessage);
        }
        if (argv.Length == 0 || a.Flags.Contains("help") || a.Flags.Contains("h")) { Console.Out.Write(UsageText()); return 0; }
        if (a.Flags.Contains("version")) { Console.Out.WriteLine("ojgen " + Util.Version); return 0; }
        if (a.Flags.Contains("list")) { Console.Out.Write(ListText()); return 0; }
        if (a.Flags.Contains("selftest")) return SelfTest.Run(log);
        int workspaceModes = WorkspaceModes(a);
        if (workspaceModes > 1) { Err("LỖI: chạy một lệnh workspace mỗi lần."); return 2; }
        if (workspaceModes == 1) return RunWorkspaceCommand(a, log);
        if (a.Kv.ContainsKey("preset-dir") || a.Kv.ContainsKey("library-search") || a.Kv.ContainsKey("library-tag") || a.Kv.ContainsKey("format") || a.Kv.ContainsKey("contest-export") && !a.Kv.ContainsKey("contest"))
        { Err("LỖI: tùy chọn workspace đang thiếu lệnh tương ứng."); return 2; }
        if (a.Flags.Contains("topics"))
        {
            if (a.Kv.ContainsKey("topic")) { Err("LỖI: không dùng --topic cùng --topics."); return 2; }
            var sources = AlgorithmTopics.All.Select(t => t.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var sourceFilter = a.Kv.GetValueOrDefault("topic-source");
            if (sourceFilter != null && !sources.Contains(sourceFilter, StringComparer.OrdinalIgnoreCase))
            {
                Err("LỖI: nguồn chủ đề không hợp lệ. Nguồn có sẵn: " + string.Join(", ", sources));
                return 2;
            }
            Console.Out.Write(ListTopicsText(sourceFilter));
            return 0;
        }
        if (a.Kv.ContainsKey("topic-source"))
        {
            Err("LỖI: --topic-source chỉ dùng cùng --topics.");
            return 2;
        }
        AlgorithmTopic? selectedTopic = null;
        if (a.Kv.TryGetValue("topic", out var topicQuery))
        {
            if (a.Kv.ContainsKey("info")) { Err("LỖI: dùng --topic hoặc --info trong một lần gọi."); return 2; }
            if (!TryResolveTopic(topicQuery, out selectedTopic, out err)) { Err("LỖI: " + err); return 2; }
            if (!HasGenerationOptions(a))
            {
                Console.Out.Write(TopicInfoText(selectedTopic!));
                return 0;
            }
            if (a.Kv.ContainsKey("regen-out") || a.Kv.ContainsKey("zip-dir"))
            {
                Err("LỖI: --topic dùng để chọn khuôn sinh test, không dùng cùng --regen-out hoặc --zip-dir.");
                return 2;
            }
        }
        if (a.Kv.TryGetValue("info", out var infoId))
        {
            var ip = Problems.Find(infoId);
            if (ip == null) { Err($"LỖI: không có dạng bài '{infoId}'.\n{ListText()}"); return 2; }
            Console.Out.Write(InfoText(ip));
            return 0;
        }

        bool auditDirMode = a.Kv.ContainsKey("audit-dir");
        bool auditMode = a.Flags.Contains("audit") || auditDirMode;
        bool auditOptionsSupplied = new[] { "validator", "checker", "checker-args", "checker-timeout", "compare-solution", "statement", "contract-suite" }.Any(a.Kv.ContainsKey);
        if (a.Flags.Contains("audit") && auditDirMode)
        {
            Err("LỖI: dùng --audit khi sinh test hoặc --audit-dir khi kiểm định thư mục có sẵn.");
            return 2;
        }
        if (!auditMode && (auditOptionsSupplied || a.Flags.Contains("allow-audit-warnings")))
        {
            Err("LỖI: tùy chọn audit cần --audit hoặc --audit-dir.");
            return 2;
        }
        if (a.Kv.TryGetValue("contract-suite", out string? contractPath) && !File.Exists(contractPath))
        { Err("LỖI: không tìm thấy --contract-suite: " + contractPath); return 2; }
        if (auditDirMode && (a.Kv.ContainsKey("out") || a.Kv.ContainsKey("regen-out") || a.Kv.ContainsKey("zip-dir")))
        {
            Err("LỖI: --audit-dir không dùng cùng --out, --regen-out hoặc --zip-dir.");
            return 2;
        }
        if (auditMode && (a.Kv.ContainsKey("regen-out") || a.Kv.ContainsKey("zip-dir")))
        {
            Err("LỖI: kiểm định phát hành không dùng cùng --regen-out hoặc --zip-dir.");
            return 2;
        }

        CliProblemProfile? profile = null;
        if (a.Kv.TryGetValue("profile", out var profilePath))
        {
            try
            {
                profile = System.Text.Json.JsonSerializer.Deserialize<CliProblemProfile>(File.ReadAllText(profilePath),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (profile == null || profile.Version != 1 || Problems.Find(profile.ProblemId) == null)
                    throw new InvalidDataException("Phiên bản hồ sơ không hỗ trợ hoặc không tìm thấy khuôn bài.");
                if (a.Kv.TryGetValue("problem", out var requestedId) && !string.Equals(requestedId, profile.ProblemId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("--problem không khớp ProblemId trong hồ sơ.");
            }
            catch (Exception ex)
            {
                Err("LỖI hồ sơ: " + ex.Message);
                return 2;
            }
        }

        AuthoringPreset? authoringPreset = null;
        TestPlanProfile? testPlan = null;
        if (a.Kv.TryGetValue("preset", out string? presetPath))
        {
            if (profile != null || a.Kv.ContainsKey("plan")) { Err("LỖI: dùng một trong --profile, --plan hoặc --preset."); return 2; }
            try
            {
                authoringPreset = AuthoringPreset.Load(presetPath);
                if (a.Kv.TryGetValue("problem", out string? presetProblem) && !string.Equals(presetProblem, authoringPreset.ProblemId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("--problem không khớp ProblemId trong preset.");
                if (authoringPreset.TestPlan.Groups.Count > 0) testPlan = authoringPreset.TestPlan;
            }
            catch (Exception ex) { Err("LỖI preset: " + ex.Message); return 2; }
            if (auditDirMode) { Err("LỖI: --preset dùng để sinh test; --audit-dir kiểm định thư mục có sẵn."); return 2; }
            auditMode |= authoringPreset.ValidatorPath.Length > 0 || authoringPreset.CheckerPath.Length > 0 ||
                authoringPreset.ContractSuitePath.Length > 0 || authoringPreset.CandidateSolution.Length > 0;
        }
        else if (a.Kv.TryGetValue("plan", out var planPath))
        {
            try { testPlan = TestPlanProfile.Load(planPath); }
            catch (Exception ex) { Err("LỖI kế hoạch test: " + ex.Message); return 2; }
            if (profile != null) { Err("LỖI: dùng --plan hoặc --profile trong một lần gọi, không dùng cùng nhau."); return 2; }
            if (auditDirMode) { Err("LỖI: --plan dùng để sinh test; --audit-dir kiểm định thư mục có sẵn."); return 2; }
        }

        var st = Settings.Load(a.Kv.GetValueOrDefault("settings"), out string? warn);
        if (!string.IsNullOrEmpty(warn)) Console.Out.WriteLine(warn);
        if (!ApplySettingsOverrides(a, st, out err)) { Err("LỖI: " + err); return 2; }
        int checkerTimeoutMs = 0;
        if (a.Kv.TryGetValue("checker-timeout", out var checkerTimeoutText))
        {
            if (!Util.ParseLL(checkerTimeoutText, out long parsedCheckerTimeout) || parsedCheckerTimeout < 1 || parsedCheckerTimeout > 3600000)
            { Err("LỖI: --checker-timeout phải là số nguyên trong [1, 3600000] ms."); return 2; }
            checkerTimeoutMs = (int)parsedCheckerTimeout;
        }
        if ((a.Kv.ContainsKey("checker-args") || a.Kv.ContainsKey("checker-timeout")) && !a.Kv.ContainsKey("checker"))
        { Err("LỖI: --checker-args và --checker-timeout cần đi cùng --checker."); return 2; }
        ZipMode zipMode = a.Flags.Contains("no-zip") ? ZipMode.No : ZipMode.Yes;
        if (a.Flags.Contains("resume") && a.Flags.Contains("force"))
        { Err("LỖI: --resume và --force không dùng cùng nhau."); return 2; }

        if (a.Kv.TryGetValue("completion", out string? shell))
        {
            string completion = CompletionText(shell);
            if (completion.Length == 0) { Err("LỖI: --completion chỉ nhận bash hoặc powershell."); return 2; }
            Console.Out.Write(completion);
            return 0;
        }
        if (a.Kv.TryGetValue("import-tests", out string? importSource))
        {
            if (!a.Kv.TryGetValue("out", out string? importDest) || a.Kv.ContainsKey("problem"))
            { Err("LỖI: --import-tests cần --out <thư mục đích mới> và không dùng --problem."); return 2; }
            try
            {
                var imported = TestPackageTools.Import(importSource, importDest, st.Prefix);
                Console.Out.WriteLine($"Đã nhập {imported.Inputs} input và {imported.Outputs} output vào {Path.GetFullPath(importDest)}.");
                foreach (string warning in imported.Warnings) Console.Out.WriteLine("  ! " + warning);
                if (a.Kv.TryGetValue("json-report", out string? jsonPath) && !WriteJsonReport(jsonPath,
                    new { exitCode = 0, imported.Inputs, imported.Outputs, imported.Warnings, destination = Path.GetFullPath(importDest) }, out string? jsonError))
                { Err(jsonError ?? "Không ghi được JSON report."); return 2; }
                return 0;
            }
            catch (Exception ex) { Err("LỖI nhập test: " + ex.Message); return 2; }
        }
        if (a.Kv.TryGetValue("compare-package", out string? packageA) || a.Kv.ContainsKey("against"))
        {
            if (packageA == null || !a.Kv.TryGetValue("against", out string? packageB))
            { Err("LỖI: dùng --compare-package <A> --against <B>."); return 2; }
            try
            {
                var diff = TestPackageTools.Compare(packageA, packageB);
                Console.Out.WriteLine($"Trùng nội dung: {diff.Identical}; thêm ở B: {diff.Added.Count}; thiếu ở B: {diff.Removed.Count}; khác nội dung: {diff.Changed.Count}.");
                foreach (string item in diff.Added) Console.Out.WriteLine("  + " + item);
                foreach (string item in diff.Removed) Console.Out.WriteLine("  - " + item);
                foreach (string item in diff.Changed) Console.Out.WriteLine("  * " + item);
                int diffExitCode = diff.Same ? 0 : 1;
                if (a.Kv.TryGetValue("json-report", out string? jsonPath) && !WriteJsonReport(jsonPath,
                    new { exitCode = diffExitCode, same = diff.Same, diff.Identical, diff.Added, diff.Removed, diff.Changed }, out string? jsonError))
                { Err(jsonError ?? "Không ghi được JSON report."); return 2; }
                return diffExitCode;
            }
            catch (Exception ex) { Err("LỖI so sánh gói test: " + ex.Message); return 2; }
        }

        if (a.Kv.TryGetValue("zip-dir", out var zdir))
        {
            bool ok = Job.ZipTestFolder(zdir, st, log, out string? zipPath);
            int code = ok ? 0 : 2;
            if (a.Kv.TryGetValue("json-report", out string? jsonPath) && !WriteJsonReport(jsonPath, new { ok, zipPath }, out string? jsonError))
            { Err(jsonError ?? "Không ghi được JSON report."); return 2; }
            return code;
        }
        if (a.Kv.TryGetValue("regen-out", out var rdir))
        {
            if (!a.Kv.TryGetValue("solution", out var rsol)) { Err("LỖI: --regen-out cần --solution <file lời giải>."); return 2; }
            var regenerated = Job.RegenerateOutputs(rdir, rsol, st, zipMode, a.Flags.Contains("rebuild"), a.Flags.Contains("zip-incomplete"), log);
            if (a.Kv.TryGetValue("json-report", out string? jsonPath) && !WriteJsonReport(jsonPath, new { exitCode = regenerated.ExitCode, status = regenerated.Status.ToString(), regenerated.OutDir, regenerated.ZipPath, regenerated.InputCount, regenerated.Report }, out string? jsonError))
            { Err(jsonError ?? "Không ghi được JSON report."); return 2; }
            return regenerated.ExitCode;
        }
        string pid;
        if (a.Kv.TryGetValue("problem", out var explicitProblemId)) pid = explicitProblemId;
        else if (profile is not null) pid = profile.ProblemId;
        else if (authoringPreset is not null) pid = authoringPreset.ProblemId;
        else if (testPlan is not null) pid = testPlan.ProblemId;
        else if (selectedTopic is not null)
        {
            pid = selectedTopic.RecommendedProblemId;
            Console.Out.WriteLine($"Chủ đề {TopicKey(selectedTopic)}: dùng khuôn input gợi ý --problem {pid}.");
        }
        else
        {
            Err("LỖI: thiếu --problem <dạng>. Dùng --list để xem các dạng bài.");
            return 2;
        }
        var prob = Problems.Find(pid);
        if (prob == null) { Err($"LỖI: không có dạng bài '{pid}'.\n{ListText()}"); return 2; }
        if (selectedTopic is not null && prob.Id != selectedTopic.RecommendedProblemId)
            Console.Out.WriteLine($"Chủ đề {TopicKey(selectedTopic)} gợi ý --problem {selectedTopic.RecommendedProblemId}; đang dùng dạng --problem {prob.Id} theo yêu cầu.");

        var job = new JobSpec { Problem = prob, Cfg = prob.DefaultConfig() };
        if (profile is not null)
        {
            foreach (var param in prob.Params)
            {
                if (param.Type == ParamType.Text && profile.Texts.TryGetValue(param.Key, out var textValue)) job.Cfg.SetText(param.Key, textValue);
                else if (param.Type != ParamType.Text && profile.Ints.TryGetValue(param.Key, out var intValue)) job.Cfg.Set(param.Key, intValue);
            }
            job.ProblemName = profile.Name;
            job.ProblemCode = profile.Code;
        }
        else if (authoringPreset is not null)
        {
            foreach (var parameter in prob.Params)
            {
                if (parameter.Type == ParamType.Text && authoringPreset.Texts.TryGetValue(parameter.Key, out string? textValue)) job.Cfg.SetText(parameter.Key, textValue);
                else if (parameter.Type != ParamType.Text && authoringPreset.Ints.TryGetValue(parameter.Key, out long intValue)) job.Cfg.Set(parameter.Key, intValue);
            }
        }
        var explicitKeys = new HashSet<string>();
        var specs = prob.Params.ToDictionary(x => x.Key);
        foreach (var k in a.Order)
        {
            if (GlobalKeys.Contains(k)) continue;
            if (!specs.TryGetValue(k, out var ps))
            {
                string names = string.Concat(prob.Params.Select(x => " --" + x.Key));
                Err($"LỖI: dạng bài '{prob.Id}' không có tham số --{k}.\n  Các tham số hợp lệ:{names}\n  Xem chi tiết: ojgen --info {prob.Id}");
                return 2;
            }
            string v = a.Kv[k];
            if (ps.Type == ParamType.Text) job.Cfg.SetText(k, v);
            else if (ps.Type == ParamType.Bool)
            {
                string b = v.ToLowerInvariant();
                if (b is "1" or "true" or "yes" or "y") job.Cfg.Set(k, 1);
                else if (b is "0" or "false" or "no" or "n") job.Cfg.Set(k, 0);
                else { Err($"LỖI: --{k} chỉ nhận 0 hoặc 1."); return 2; }
            }
            else
            {
                if (!Util.ParseLL(v, out long x))
                {
                    Err($"LỖI: --{k} = '{v}' không phải số nguyên hợp lệ (hoặc vượt phạm vi long long).");
                    return 2;
                }
                job.Cfg.Set(k, x);
            }
            explicitKeys.Add(k);
        }
        prob.FinalizeCliConfig(job.Cfg, explicitKeys);
        if (Generator.ValidateFullConfig(prob, job.Cfg) is { } ce) { Err("LỖI cấu hình: " + ce); return 2; }
        if (testPlan != null && explicitKeys.Count > 0)
        { Err("LỖI: --plan đã chứa cấu hình cho từng nhóm; không truyền thêm tham số riêng của khuôn."); return 2; }

        long tests = authoringPreset?.TestCount ?? 10;
        if (testPlan != null && a.Kv.ContainsKey("tests"))
        { Err("LỖI: số test đã được lưu trong --plan; bỏ --tests."); return 2; }
        if (a.Kv.TryGetValue("tests", out var ts) && (!Util.ParseLL(ts, out tests) || tests < 1 || tests > Generator.MaxTests))
        {
            Err($"LỖI: --tests phải là số nguyên trong [1, {Generator.MaxTests}].");
            return 2;
        }
        job.TestCount = (int)tests;
        if (testPlan != null)
        {
            try
            {
                job.Groups = testPlan.ToGroups(prob);
                job.TestCount = testPlan.TotalTestCount;
                job.RequiredCoverageTags = testPlan.RequiredCoverageTags?.ToList() ?? new List<string>();
            }
            catch (Exception ex) { Err("LỖI kế hoạch test: " + ex.Message); return 2; }
        }
        if (a.Kv.TryGetValue("coverage-required", out string? requiredTagText))
        {
            if (TestCoverageTags.Parse(requiredTagText, out var parsedRequiredTags) is { } coverageTagError)
            { Err("LỖI nhãn độ phủ: " + coverageTagError); return 2; }
            job.RequiredCoverageTags = job.RequiredCoverageTags.Concat(parsedRequiredTags)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        else if (authoringPreset != null && job.RequiredCoverageTags.Count == 0)
            job.RequiredCoverageTags = authoringPreset.RequiredCoverageTags.ToList();
        if (a.Kv.TryGetValue("seed", out var ss))
        {
            if (!Util.ParseULL(ss, out ulong sv)) { Err("LỖI: --seed phải là số nguyên không âm."); return 2; }
            job.Seed = sv;
        }
        else if (authoringPreset?.SeedPolicy == "Fixed") job.Seed = authoringPreset.Seed;
        if (a.Flags.Contains("resume") && job.Seed == null)
        { Err("LỖI: --resume cần --seed để tái tạo đúng cùng cấu hình và thứ tự sinh."); return 2; }
        if (a.Kv.TryGetValue("solution", out var sol))
        {
            job.SolutionPath = sol;
            if (sol.Trim().Length == 0) { Err("LỖI: --solution rỗng."); return 2; }
        }
        else if (profile is not null) job.SolutionPath = profile.SolutionPath;
        else if (authoringPreset is not null) job.SolutionPath = authoringPreset.ReferenceSolution;

        if (auditDirMode)
        {
            string auditDir = a.Kv["audit-dir"];
            if (!Directory.Exists(auditDir)) { Err("LỖI: không tìm thấy thư mục --audit-dir: " + auditDir); return 2; }
            int inputCount = FileManager.ListFilesWithExt(auditDir, ".inp").Count;
            var auditSpec = new JobSpec
            {
                Problem = prob,
                Cfg = job.Cfg.Clone(),
                TestCount = a.Kv.ContainsKey("tests") ? job.TestCount : inputCount,
                GeneratedTestCount = a.Kv.ContainsKey("tests") ? job.TestCount : inputCount,
                Seed = job.Seed,
                OutDir = auditDir,
                RequiredCoverageTags = job.RequiredCoverageTags.ToList(),
            };
            var auditOptions = new CliAuditOptions
            {
                Problem = prob,
                Config = job.Cfg,
                Settings = st,
                TestDirectory = auditDir,
                ExpectedTestCount = a.Kv.ContainsKey("tests") ? job.TestCount : 0,
                ReferenceSolution = job.SolutionPath,
                ValidatorPath = a.Kv.GetValueOrDefault("validator", ""),
                CheckerPath = a.Kv.GetValueOrDefault("checker", ""),
                CheckerArguments = a.Kv.GetValueOrDefault("checker-args", "{input} {output} {answer}"),
                CheckerTimeoutMs = checkerTimeoutMs,
                ContractSuitePath = a.Kv.GetValueOrDefault("contract-suite", ""),
                CandidateSolution = a.Kv.GetValueOrDefault("compare-solution", ""),
                StatementPath = a.Kv.GetValueOrDefault("statement", ""),
                Seed = job.Seed,
                DifferentialSeed = job.Seed ?? Generator.RandomSeed(),
                GeneratedSpec = auditSpec,
                RequiredCoverageTags = job.RequiredCoverageTags,
            };
            CliAuditReport auditReport = CliReleaseAudit.Run(auditOptions, log);
            if (auditReport.Cancelled)
                return FinishWithJson(a, 1, new { exitCode = 1, cancelled = true, audit = auditReport.Items, warnings = auditReport.WarningCount });
            if (auditReport.HasErrors || (zipMode == ZipMode.Yes && auditReport.WarningCount > 0 && !a.Flags.Contains("allow-audit-warnings")))
            {
                Err("LỖI: không tạo ZIP. Sửa lỗi hoặc dùng --allow-audit-warnings để chấp nhận các cảnh báo đã xem trong AUDIT_REPORT.txt.");
                return FinishWithJson(a, 2, new { exitCode = 2, audit = auditReport.Items, warnings = auditReport.WarningCount, zipPath = (string?)null });
            }
            string? auditZip = null;
            if (zipMode == ZipMode.Yes && !Job.ZipTestFolder(auditDir, st, log, out auditZip))
                return FinishWithJson(a, 2, new { exitCode = 2, audit = auditReport.Items, warnings = auditReport.WarningCount, zipPath = auditZip });
            return FinishWithJson(a, 0, new { exitCode = 0, audit = auditReport.Items, warnings = auditReport.WarningCount, zipPath = auditZip });
        }
        string outDir = a.Kv.TryGetValue("out", out var od) ? od : Path.Combine(st.OutputRoot, prob.Id);
        if (a.Flags.Contains("resume"))
        {
            try { Directory.CreateDirectory(outDir); }
            catch (Exception ex) { Err("LỖI: không chuẩn bị được thư mục tiếp tục: " + ex.Message); return 2; }
        }
        else if (!Job.PrepareOutDir(ref outDir, a.Flags.Contains("force") ? ExistingPolicy.Overwrite : ExistingPolicy.NewDir, log, out string? outDirError))
        {
            Err("LỖI: " + (outDirError ?? "Không thể chuẩn bị thư mục đầu ra."));
            return 2;
        }
        job.OutDir = outDir;
        job.Zip = zipMode;
        job.ZipIncomplete = a.Flags.Contains("zip-incomplete");
        job.ForceRebuild = a.Flags.Contains("rebuild");
        job.ResumeGeneration = a.Flags.Contains("resume");
        if (!auditMode)
        {
            JobResult generatedOnly = Job.Run(job, st, log);
            if (a.Kv.TryGetValue("json-report", out string? jsonPath) && !WriteJsonReport(jsonPath, new
                { exitCode = generatedOnly.ExitCode, status = generatedOnly.Status.ToString(), generatedOnly.Seed, generatedOnly.OutDir, generatedOnly.ZipPath,
                  generatedOnly.InputCount, generatedOnly.Coverage, generatedOnly.Report }, out string? jsonError))
            { Err(jsonError ?? "Không ghi được JSON report."); return 2; }
            return generatedOnly.ExitCode;
        }

        job.Zip = ZipMode.No;
        JobResult generated = Job.Run(job, st, log);
        if (generated.ExitCode >= 2 || generated.Cancelled)
            return FinishWithJson(a, generated.ExitCode, new { exitCode = generated.ExitCode, cancelled = generated.Cancelled,
                status = generated.Status.ToString(), generated.Seed, generated.OutDir, generated.ZipPath, generated.InputCount, generated.Report });
        job.Seed = generated.Seed;
        var generatedAuditOptions = new CliAuditOptions
        {
            Problem = prob,
            Config = job.Cfg,
            Settings = st,
            TestDirectory = outDir,
            ExpectedTestCount = job.TestCount,
            ReferenceSolution = job.SolutionPath,
            ValidatorPath = a.Kv.GetValueOrDefault("validator", authoringPreset?.ValidatorPath ?? ""),
            CheckerPath = a.Kv.GetValueOrDefault("checker", authoringPreset?.CheckerPath ?? ""),
            CheckerArguments = a.Kv.GetValueOrDefault("checker-args", "{input} {output} {answer}"),
            CheckerTimeoutMs = checkerTimeoutMs > 0 ? checkerTimeoutMs : authoringPreset?.CheckerTimeoutMs ?? 0,
            ContractSuitePath = a.Kv.GetValueOrDefault("contract-suite", authoringPreset?.ContractSuitePath ?? ""),
            CandidateSolution = a.Kv.GetValueOrDefault("compare-solution", authoringPreset?.CandidateSolution ?? ""),
            StatementPath = a.Kv.GetValueOrDefault("statement", ""),
            Seed = generated.Seed,
            DifferentialSeed = generated.Seed,
            GeneratedSpec = job,
            RequiredCoverageTags = job.RequiredCoverageTags,
        };
        CliAuditReport generatedAudit = CliReleaseAudit.Run(generatedAuditOptions, log);
        if (generatedAudit.Cancelled)
            return FinishWithJson(a, 1, new { exitCode = 1, cancelled = true, status = generated.Status.ToString(), generated.Seed,
                generated.OutDir, generated.InputCount, generated.Coverage, generated.Report, audit = generatedAudit.Items, warnings = generatedAudit.WarningCount });
        if (generated.ExitCode != 0)
            return FinishWithJson(a, generated.ExitCode, new { exitCode = generated.ExitCode, status = generated.Status.ToString(), generated.Seed,
                generated.OutDir, generated.InputCount, generated.Coverage, generated.Report, audit = generatedAudit.Items, warnings = generatedAudit.WarningCount });
        if (generatedAudit.HasErrors || (zipMode == ZipMode.Yes && generatedAudit.WarningCount > 0 && !a.Flags.Contains("allow-audit-warnings")))
        {
            Err("LỖI: kiểm định chưa được chấp nhận nên không tạo ZIP. Xem AUDIT_REPORT.txt; dùng --allow-audit-warnings nếu đã rà soát các cảnh báo.");
            return FinishWithJson(a, 2, new { exitCode = 2, status = generated.Status.ToString(), generated.Seed, generated.OutDir,
                generated.InputCount, generated.Coverage, generated.Report, audit = generatedAudit.Items, warnings = generatedAudit.WarningCount, zipPath = (string?)null });
        }
        string? finalZipPath = generated.ZipPath;
        if (zipMode == ZipMode.Yes && !Job.ZipTestFolder(outDir, st, log, out finalZipPath))
            return FinishWithJson(a, 2, new { exitCode = 2, status = generated.Status.ToString(), generated.Seed, generated.OutDir,
                generated.InputCount, generated.Coverage, generated.Report, audit = generatedAudit.Items, warnings = generatedAudit.WarningCount, zipPath = finalZipPath });
        return FinishWithJson(a, 0, new { exitCode = 0, status = generated.Status.ToString(), generated.Seed, generated.OutDir,
            zipPath = finalZipPath, generated.InputCount, generated.Coverage, generated.Report, audit = generatedAudit.Items, warnings = generatedAudit.WarningCount });
    }
}
