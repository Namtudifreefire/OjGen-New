using OjGen.Core;

namespace OjGen.Gui;

internal sealed record ScaffoldTemplate(string Key, string DisplayName, string FileName, string Source, string Readme);

internal static class AuthoringScaffolds
{
    public static IReadOnlyList<ScaffoldTemplate> Build(Problem problem, Config config)
    {
        string problemHint = string.Join("\n", problem.FormatDescription.TrimEnd().Split('\n').Select(line => "// " + line));
        string configHint = string.Join("\n", problem.DescribeConfig(config).TrimEnd().Split('\n').Select(line => "// " + line));
        var templates = new List<ScaffoldTemplate>
        {
            new ScaffoldTemplate("validator_cpp", "Validator C++ (standalone)", "validator.cpp",
                "#include <bits/stdc++.h>\nusing namespace std;\n\nint main(int argc, char** argv) {\n    ios::sync_with_stdio(false);\n    cin.tie(nullptr);\n\n    ifstream file;\n    if (argc > 1) file.open(argv[1]);\n    istream& in = (argc > 1) ? static_cast<istream&>(file) : cin;\n    if (!in) { cerr << \"cannot open input\\n\"; return 1; }\n\n    // TODO: đọc input theo cấu trúc bên dưới và kiểm tra từng giới hạn.\n" + problemHint + "\n" + configHint + "\n\n    // Ví dụ: long long n; if (!(in >> n)) return 1;\n    // TODO: từ chối token thừa và dữ liệu không hợp lệ.\n    string extra;\n    if (in >> extra) { cerr << \"extra token\\n\"; return 1; }\n    cerr << \"validator incomplete: finish all checks before use\\n\";\n    return 1;\n}\n",
                "Validator standalone: nhận đường dẫn input ở argv[1], hoặc đọc stdin nếu không có đối số. Template từ chối input cho tới khi bạn hoàn thiện TODO và đổi guard cuối thành kết quả thành công."),
            new ScaffoldTemplate("validator_py", "Validator Python", "validator.py",
                "#!/usr/bin/env python3\nimport sys\nfrom pathlib import Path\n\n" + string.Join("\n", problem.FormatDescription.TrimEnd().Split('\n').Select(line => "# " + line)) + "\n\nraw = Path(sys.argv[1]).read_text(encoding=\"utf-8\") if len(sys.argv) > 1 else sys.stdin.read()\nlines = raw.splitlines()\n\n# TODO: parse exact lines/tokens and enforce the official constraints.\n# Example: if not lines: raise SystemExit(1)\n# TODO: reject missing/extra tokens and malformed values.\nraise SystemExit(\"validator incomplete: finish all checks before use\")\n",
                "Validator Python: nhận đường dẫn input trong argv[1] hoặc stdin. Template từ chối input cho tới khi bạn hoàn thiện TODO và thay guard cuối bằng kết quả thành công."),
            new ScaffoldTemplate("checker_cpp", "Checker C++ (token compare starter)", "checker.cpp",
                "#include <bits/stdc++.h>\nusing namespace std;\n\nint main(int argc, char** argv) {\n    if (argc != 4) { cerr << \"usage: checker input contestant_output jury_output\\n\"; return 2; }\n    ifstream input(argv[1]), contestant(argv[2]), jury(argv[3]);\n    if (!input || !contestant || !jury) { cerr << \"cannot open checker files\\n\"; return 2; }\n\n    // TODO: dùng input để kiểm tra các ràng buộc của output nếu cần.\n    vector<string> got, expected;\n    for (string token; contestant >> token;) got.push_back(token);\n    for (string token; jury >> token;) expected.push_back(token);\n    if (got != expected) { cout << \"wrong answer\\n\"; return 1; }\n    cout << \"accepted\\n\";\n    return 0;\n}\n",
                "Checker C++ starter: argv[1]=input, argv[2]=output của thí sinh, argv[3]=output chuẩn. Mặc định so sánh token chính xác; sửa TODO để chấp nhận nhiều đáp án hoặc sai số. Mã thoát 0=accepted, 1=wrong answer, 2=checker error."),
            new ScaffoldTemplate("checker_py", "Checker Python (token compare starter)", "checker.py",
                "#!/usr/bin/env python3\nimport sys\nfrom pathlib import Path\n\nif len(sys.argv) != 4:\n    print(\"usage: checker.py input contestant_output jury_output\", file=sys.stderr)\n    raise SystemExit(2)\ninput_text, contestant_text, jury_text = [Path(path).read_text(encoding=\"utf-8\") for path in sys.argv[1:4]]\n\n# TODO: use input_text to validate contestant output where needed.\nif contestant_text.split() != jury_text.split():\n    print(\"wrong answer\")\n    raise SystemExit(1)\nprint(\"accepted\")\nraise SystemExit(0)\n",
                "Checker Python starter: argv[1]=input, argv[2]=output của thí sinh, argv[3]=output chuẩn. Mặc định so sánh token; sửa TODO cho checker đặc thù. Mã thoát 0=accepted, 1=wrong answer, 2=checker error."),
            new ScaffoldTemplate("checker_testlib", "Checker C++ (testlib)", "checker.cpp",
                "#include \"testlib.h\"\n\nint main(int argc, char* argv[]) {\n    registerTestlibCmd(argc, argv);\n    // inf: input, ouf: output thí sinh, ans: đáp án chuẩn.\n    // Ví dụ nhiều đáp án hợp lệ: kiểm tra ouf.readInt() thỏa ràng buộc từ inf,\n    // không bắt buộc bằng ans. Dùng quitf(_wa, ...) nếu sai, quitf(_ok, ...) nếu đạt.\n    // TODO: phân tích input/output theo đề, kiểm tra token thừa bằng ouf.readEof().\n    quitf(_fail, \"hoàn thiện checker trước khi sử dụng\");\n}\n",
                "Checker testlib: cần testlib.h trên include path. quitf(_ok)=AC, quitf(_wa)=WA, quitf(_fail)=lỗi checker. Có thể chấp nhận nhiều đáp án bằng cách kiểm tra tính chất thay vì so sánh ans."),
            new ScaffoldTemplate("checker_py_float", "Checker Python (sai số số thực)", "checker_float.py",
                "#!/usr/bin/env python3\nimport math, sys\nfrom pathlib import Path\n\nif len(sys.argv) != 4:\n    raise SystemExit(2)\n_, contestant, jury = sys.argv[1:]\ngot = Path(contestant).read_text(encoding=\"utf-8\").split()\nexpected = Path(jury).read_text(encoding=\"utf-8\").split()\nif len(got) != len(expected):\n    raise SystemExit(1)\ntry:\n    for a, b in zip(got, expected):\n        x, y = float(a), float(b)\n        if not (math.isfinite(x) and math.isfinite(y) and math.isclose(x, y, rel_tol=1e-6, abs_tol=1e-8)):\n            raise SystemExit(1)\nexcept ValueError:\n    if got != expected:\n        raise SystemExit(1)\nraise SystemExit(0)\n",
                "So sánh từng token số thực bằng rel_tol=1e-6, abs_tol=1e-8; token không phải số so sánh chính xác. Điều chỉnh sai số theo đề."),
            new ScaffoldTemplate("checker_py_unordered", "Checker Python (token không thứ tự)", "checker_unordered.py",
                "#!/usr/bin/env python3\nimport sys\nfrom collections import Counter\nfrom pathlib import Path\n\nif len(sys.argv) != 4:\n    raise SystemExit(2)\ncontestant = Path(sys.argv[2]).read_text(encoding=\"utf-8\").split()\njury = Path(sys.argv[3]).read_text(encoding=\"utf-8\").split()\nraise SystemExit(0 if Counter(contestant) == Counter(jury) else 1)\n",
                "So sánh đa tập token, bỏ qua thứ tự nhưng vẫn phát hiện thiếu/thừa phần tử. Nếu thứ tự không liên quan nhưng số lần lặp cũng không quan trọng, đổi Counter thành set."),
            new ScaffoldTemplate("validator_profile_py", "Validator Python theo khuôn đang chọn", "validator.py",
                "#!/usr/bin/env python3\nimport sys\nfrom pathlib import Path\n\n# Dạng input cụ thể của khuôn:\n" + string.Join("\n", problem.FormatDescription.TrimEnd().Split('\n').Select(line => "# " + line)) + "\n\n# Cấu hình generator dùng làm mốc; thay bằng cận chính thức nếu chúng khác nhau:\n" + string.Join("\n", problem.DescribeConfig(config).TrimEnd().Split('\n').Select(line => "# " + line)) + "\n\nraw = Path(sys.argv[1]).read_text(encoding=\"utf-8\") if len(sys.argv) > 1 else sys.stdin.read()\nlines = raw.splitlines()\n# TODO: parse đúng từng dòng và kiểm tra tất cả cận theo khuôn.\n# Không dùng validator này cho tới khi hoàn thành TODO và thêm kiểm tra token thừa.\nraise SystemExit(\"validator incomplete\")\n",
                "Skeleton được điền sẵn định dạng và cấu hình của khuôn hiện tại. OjGen có thể tạo contract valid/invalid theo khuôn và chạy thử validator ở nút bên dưới."),
        };
        return templates;
    }
}
