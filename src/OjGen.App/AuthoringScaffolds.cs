using OjGen.Core;

namespace OjGen.Gui;

internal sealed record ScaffoldTemplate(string Key, string DisplayName, string FileName, string Source, string Readme);

internal static class AuthoringScaffolds
{
    public static IReadOnlyList<ScaffoldTemplate> Build(Problem problem, Config config)
    {
        string problemHint = string.Join("\n", problem.FormatDescription.TrimEnd().Split('\n').Select(line => "// " + line));
        string configHint = string.Join("\n", problem.DescribeConfig(config).TrimEnd().Split('\n').Select(line => "// " + line));
        return new[]
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
        };
    }
}
