#!/usr/bin/env bash
# tests/run_tests.sh - Kiểm thử đầu-cuối cho ojgen (bản C#). Chạy từ thư mục gốc project:
#   bash tests/run_tests.sh
# Cần: .NET SDK 10, g++ (để biên dịch lời giải C++), python3 (cho lời giải .py, có thể thiếu).
# Biến môi trường tùy chọn:
#   CPP_REF=/đường/dẫn/ojgen_cpp   -> so khớp từng byte với bản C++ gốc
set -u
cd "$(dirname "$0")/.."

echo "== Build"
dotnet build src/OjGen.Cli -c Release -nologo -v q > /dev/null || { echo "Build thất bại"; exit 1; }
BIN=(dotnet src/OjGen.Cli/bin/Release/net10.0/ojgen.dll)

PASS=0
FAIL=0
ok()   { PASS=$((PASS + 1)); echo "  [OK]   $1"; }
bad()  { FAIL=$((FAIL + 1)); echo "  [FAIL] $1"; }
expect_exit() {  # expect_exit <mã mong đợi> <mô tả> <lệnh...>
    local want=$1 what=$2; shift 2
    "$@" > "$LOG" 2>&1
    local got=$?
    if [[ $got -eq $want ]]; then ok "$what (exit $got)"; else bad "$what: exit $got, mong đợi $want (xem $LOG)"; fi
}
count() { ls "$1" 2>/dev/null | grep -c "$2$" || true; }

T="${OJGEN_TEST_DIR:-}"
if [[ -z "$T" ]]; then
    T=$(mktemp -d "${TMPDIR:-/tmp}/OjGen-tests.XXXXXX") || exit 1
else
    if [[ -e "$T" ]]; then echo "Thư mục kiểm thử đã tồn tại, không ghi đè: $T"; exit 2; fi
    mkdir -p "$T" || exit 1
fi
LOG="$T/last.log"
export OJGEN_SELFTEST_ROOT="${OJGEN_SELFTEST_ROOT:-output/.selftest}"
COMMON=(--no-zip)
SOL=tests/solutions

echo "== Tự kiểm tra nội bộ"
expect_exit 0 "--selftest" "${BIN[@]}" --selftest

echo "== Lệnh ví dụ"
expect_exit 0 "--version" "${BIN[@]}" --version
expect_exit 0 "--list" "${BIN[@]}" --list
expect_exit 0 "--info graph" "${BIN[@]}" --info graph
expect_exit 0 "--problem array --tests 20 --seed 2024" "${BIN[@]}" --problem array --tests 20 --seed 2024 --out "$T/array"
[[ $(count "$T/array" .inp) -eq 20 && -f "$T/array.zip" ]] && ok "array: 20 file .inp + ZIP" || bad "array: thiếu file"
[[ -f "$T/array/SHA256SUMS.txt" && $(wc -l < "$T/array/SHA256SUMS.txt") -eq 20 ]] && ok "array: manifest SHA-256 có đủ 20 input" || bad "array: manifest SHA-256 sai"
expect_exit 0 "--problem custom --tests 100 --seed 2024" "${BIN[@]}" --problem custom --tests 100 --seed 2024 --out "$T/custom"
[[ $(count "$T/custom" .inp) -eq 100 ]] && ok "custom: 100 file .inp" || bad "custom: số file sai"

echo "== Tái tạo bằng seed"
"${BIN[@]}" --problem custom --tests 100 --seed 2024 --out "$T/custom_again" "${COMMON[@]}" > /dev/null
diff -r -x ojgen_info.txt -x ojgen_provenance.json -x '*.zip' "$T/custom" "$T/custom_again" > /dev/null && ok "cùng seed -> cùng bộ test" || bad "cùng seed nhưng khác dữ liệu"

echo "== Lưu và tái sử dụng kế hoạch nhóm"
expect_exit 0 "--plan sinh đúng số test của các nhóm" "${BIN[@]}" --plan tests/fixtures/array-plan.ojplan.json --seed 2024 --out "$T/plan" --no-zip
[[ $(count "$T/plan" .inp) -eq 5 ]] && grep -q "Nhỏ" "$T/plan/ojgen_info.txt" && grep -q "Stress" "$T/plan/ojgen_info.txt" && grep -q "Nhãn độ phủ bắt buộc: small, stress" "$T/plan/ojgen_info.txt" && ok "plan: nhóm, thứ tự, nhãn và tổng test được áp dụng" || bad "plan: thiếu nhóm, nhãn hoặc sai số test"
expect_exit 0 "--plan tái lập cùng seed" "${BIN[@]}" --plan tests/fixtures/array-plan.ojplan.json --seed 2024 --out "$T/plan_again" --no-zip
diff -r -x ojgen_info.txt -x ojgen_provenance.json "$T/plan" "$T/plan_again" > /dev/null && ok "plan: nạp lại cho bộ input tái lập" || bad "plan: dữ liệu khác khi tái sử dụng"
expect_exit 2 "--plan từ chối --tests ngoài kế hoạch" "${BIN[@]}" --plan tests/fixtures/array-plan.ojplan.json --tests 5
expect_exit 2 "--plan từ chối khuôn không khớp" "${BIN[@]}" --problem graph --plan tests/fixtures/array-plan.ojplan.json
expect_exit 0 "--profile mở lại cấu hình bài đã lưu" "${BIN[@]}" --profile tests/fixtures/single-n.ojproblem.json --tests 3 --seed 14 --no-zip --out "$T/profile"
[[ $(count "$T/profile" .inp) -eq 3 ]] && grep -q "Hồ sơ hồi quy" "$T/profile/ojgen_info.txt" && ok "profile: cấu hình và thông tin bài được nạp" || bad "profile: không nạp đúng hồ sơ"

echo "== Resume, import, package diff và báo cáo JSON"
expect_exit 2 "--resume yêu cầu seed cố định" "${BIN[@]}" --problem array --tests 3 --resume --out "$T/resume_missing_seed"
expect_exit 0 "tạo bộ test có seed trước khi kiểm tra resume" "${BIN[@]}" --problem array --tests 3 --seed 77 --no-zip --out "$T/resume_done"
expect_exit 2 "--resume từ chối thư mục hoàn tất không có checkpoint" "${BIN[@]}" --problem array --tests 3 --seed 77 --resume --no-zip --out "$T/resume_done"
mkdir -p "$T/resume_stale_output"
printf 'output cũ\n' > "$T/resume_stale_output/test01.out"
expect_exit 2 "--resume từ chối output cũ nếu không có lời giải để tạo lại" "${BIN[@]}" --problem single_n --tests 3 --seed 78 --resume --no-zip --out "$T/resume_stale_output"
if command -v python3 > /dev/null; then
    export OJGEN_TEST_RESUME_COUNTER="$T/resume_counter"
    printf '0\n' > "$OJGEN_TEST_RESUME_COUNTER"
    expect_exit 1 "--resume fixture giữ trạng thái khi một test lỗi" "${BIN[@]}" --problem single_n --tests 3 --seed 79 --solution "$SOL/resume_counter.py" --resume --no-zip --out "$T/resume_output"
    [[ $(count "$T/resume_output" .out) -eq 2 && -f "$T/resume_output/test01.out" && -f "$T/resume_output/test03.out" && ! -f "$T/resume_output/test02.out" && -f "$T/resume_output/.ojgen_output_checkpoint.json" ]] && ok "resume output: giữ checkpoint và cả output hợp lệ quanh test lỗi" || bad "resume output: thiếu output hợp lệ hoặc checkpoint sau lỗi"
    cp "$T/resume_output/test01.out" "$T/resume_first_output"
    cp "$T/resume_output/test03.out" "$T/resume_third_output"
    expect_exit 0 "--resume tiếp tục output còn thiếu" "${BIN[@]}" --problem single_n --tests 3 --seed 79 --solution "$SOL/resume_counter.py" --resume --no-zip --out "$T/resume_output"
    [[ $(count "$T/resume_output" .out) -eq 3 && ! -f "$T/resume_output/.ojgen_output_checkpoint.json" ]] && ok "resume output: hoàn tất các output còn thiếu" || bad "resume output: không hoàn tất"
    cmp -s "$T/resume_first_output" "$T/resume_output/test01.out" && grep -q '^1:' "$T/resume_output/test01.out" && cmp -s "$T/resume_third_output" "$T/resume_output/test03.out" && grep -q '^3:' "$T/resume_output/test03.out" && grep -q '^4:' "$T/resume_output/test02.out" && ok "resume output: tái sử dụng output checkpoint và chỉ chạy lại test lỗi" || bad "resume output: chạy lại output đã khớp hoặc không tạo lại đúng test lỗi"
    unset OJGEN_TEST_RESUME_COUNTER
else
    echo "  (bỏ qua: không có python3 để kiểm tra tiếp tục output)"
fi
mkdir -p "$T/oj_source/data/sample" "$T/oj_source/data/secret"
printf '3\n1 2 3\n' > "$T/oj_source/data/sample/01.in"
printf '6\n' > "$T/oj_source/data/sample/01.ans"
printf '4\n1 2 3 4\n' > "$T/oj_source/data/secret/01.in"
printf '10\n' > "$T/oj_source/data/secret/01.ans"
expect_exit 0 "nhập .in/.ans từ thư mục" "${BIN[@]}" --import-tests "$T/oj_source" --out "$T/imported" --json-report "$T/import.json"
[[ $(count "$T/imported" .inp) -eq 2 && $(count "$T/imported" .out) -eq 2 && -f "$T/imported/SHA256SUMS.txt" ]] && ok "import: giữ đủ sample/secret trùng basename và tạo checksum" || bad "import: thiếu test hoặc checksum"
grep -q '"Inputs": 2' "$T/import.json" && grep -q '"Outputs": 2' "$T/import.json" && ok "import: JSON report ghi số lượng" || bad "import: JSON report sai"
expect_exit 0 "so sánh package với chính nó" "${BIN[@]}" --compare-package "$T/imported" --against "$T/imported" --json-report "$T/compare_same.json"
grep -q '"same": true' "$T/compare_same.json" && ok "diff: JSON đánh dấu hai package giống nhau" || bad "diff: JSON same sai"
cp -R "$T/imported" "$T/imported_changed"
for output in "$T/imported_changed"/*.out; do printf '7\n' > "$output"; done
expect_exit 1 "so sánh phát hiện output đã đổi" "${BIN[@]}" --compare-package "$T/imported" --against "$T/imported_changed" --json-report "$T/compare_changed.json"
grep -q '"Changed"' "$T/compare_changed.json" && ok "diff: JSON liệt kê test thay đổi" || bad "diff: thiếu danh sách thay đổi"
expect_exit 0 "completion Bash" "${BIN[@]}" --completion bash
grep -q -- '--resume' "$LOG" && grep -q -- '--plugin' "$LOG" && ok "completion: có tùy chọn resume và plugin" || bad "completion: thiếu tùy chọn"
expect_exit 0 "JSON report khi sinh test" "${BIN[@]}" --problem single_n --tests 2 --seed 8 --no-zip --out "$T/json_job" --json-report "$T/generation.json"
grep -q '"exitCode": 0' "$T/generation.json" && grep -q '"InputCount": 2' "$T/generation.json" && ok "generation: JSON report có kết quả và số input" || bad "generation: JSON report sai"

echo "== Lời giải chuẩn cho từng module"
for m in single_n two_ints array queries string custom graph; do
    expect_exit 0 "$m + lời giải mẫu" "${BIN[@]}" --problem $m --tests 15 --seed 1 --solution "examples/solutions/${m}_sol.cpp" --out "$T/sol_$m"
    [[ $(count "$T/sol_$m" .out) -eq 15 && -f "$T/sol_$m.zip" ]] && ok "$m: 15 file .out + ZIP" || bad "$m: thiếu .out/ZIP"
done
expect_exit 0 "dùng lại bản biên dịch" "${BIN[@]}" --problem single_n --tests 3 --seed 1 --solution examples/solutions/single_n_sol.cpp --out "$T/cache" "${COMMON[@]}"
grep -q "dùng lại" "$LOG" && ok "bản biên dịch được dùng lại (cache)" || bad "không dùng cache biên dịch"

echo "== Giới hạn riêng từng vị trí"
expect_exit 0 "custom --bounds 1:10,50:100,-20:20" "${BIN[@]}" --problem custom --tests 60 --seed 5 --bounds "1:10,50:100,-20:20" --out "$T/bounds" "${COMMON[@]}"
viol=$(cat "$T"/bounds/*.inp | awk '{ if ($1<1||$1>10||$2<50||$2>100||$3<-20||$3>20||NF!=3) c++ } END { print c+0 }')
[[ "$viol" == "0" ]] && ok "60 test đều đúng giới hạn từng vị trí" || bad "$viol dòng vi phạm giới hạn"

echo "== Lời giải lỗi"
expect_exit 1 "lỗi biên dịch" "${BIN[@]}" --problem custom --tests 3 --solution $SOL/bad_compile.cpp --out "$T/ce" "${COMMON[@]}"
grep -q "BIÊN DỊCH THẤT BẠI" "$LOG" && [[ $(count "$T/ce" .out) -eq 0 ]] && ok "báo lỗi biên dịch, không tạo .out" || bad "xử lý lỗi biên dịch"
expect_exit 1 "crash ở một số test" "${BIN[@]}" --problem custom --tests 12 --seed 7 --n 3 --min 1 --max 8 --solution $SOL/crash_sol.cpp --out "$T/re" "${COMMON[@]}"
nin=$(count "$T/re" .inp); nout=$(count "$T/re" .out)
[[ $nin -eq 12 && $nout -lt 12 && $nout -gt 0 ]] && ok "test lỗi không có .out ($nout/12 có .out)" || bad "crash: $nout/12 .out"
expect_exit 1 "quá thời gian" "${BIN[@]}" --problem custom --tests 4 --seed 3 --n 2 --min 1 --max 9 --timeout 500 --solution $SOL/tle_sol.cpp --out "$T/tle" "${COMMON[@]}"
grep -q "TLE" "$LOG" && ok "báo TLE" || bad "không báo TLE"
expect_exit 1 "output vô hạn" "${BIN[@]}" --problem single_n --tests 2 --max-output-mb 2 --solution $SOL/big_output.cpp --out "$T/ole" "${COMMON[@]}"
grep -q "OLE" "$LOG" && ok "báo OLE" || bad "không báo OLE"
expect_exit 1 "mã thoát khác 0" "${BIN[@]}" --problem single_n --tests 2 --solution $SOL/nonzero_exit.cpp --out "$T/nz" "${COMMON[@]}"
expect_exit 0 "lời giải bỏ qua input (cảnh báo)" "${BIN[@]}" --problem single_n --tests 5 --solution $SOL/stdin_ignored.cpp --out "$T/ign" "${COMMON[@]}"
grep -q "giống hệt nhau" "$LOG" && ok "cảnh báo output giống hệt nhau" || bad "thiếu cảnh báo output giống nhau"
expect_exit 0 "freopen bọc #ifndef OJGEN" "${BIN[@]}" --problem custom --tests 3 --solution $SOL/freopen_sol.cpp --out "$T/fre" "${COMMON[@]}"

echo "== Lời giải Python"
if command -v python3 > /dev/null; then
    expect_exit 0 "lời giải .py" "${BIN[@]}" --problem custom --tests 6 --seed 9 --solution $SOL/sum_sol.py --out "$T/py" "${COMMON[@]}"
    [[ $(count "$T/py" .out) -eq 6 ]] && ok ".py: 6 file .out" || bad ".py: thiếu .out"
    expect_exit 1 ".py lỗi một số test" "${BIN[@]}" --problem custom --tests 12 --seed 11 --n 2 --min 1 --max 9 --solution $SOL/crash_sol.py --out "$T/pyre" "${COMMON[@]}"
    grep -q "boom" "$LOG" && ok ".py: hiện stderr của lời giải" || bad ".py: không hiện stderr"
else
    echo "  (bỏ qua: không có python3)"
fi

echo "== Lời giải Java và C# đơn file"
if command -v java > /dev/null; then
    expect_exit 0 "Java source launcher (.java)" "${BIN[@]}" --problem single_n --tests 3 --seed 4 --solution tests/solutions/EchoSingleN.java --out "$T/java" "${COMMON[@]}"
    [[ $(count "$T/java" .out) -eq 3 ]] && ok "Java: đủ 3 file .out" || bad "Java: thiếu .out"
else
    echo "  (bỏ qua: không có Java runtime)"
fi
expect_exit 0 "C# đơn file (.cs)" "${BIN[@]}" --problem single_n --tests 3 --seed 4 --solution tests/solutions/echo_single_n.cs --out "$T/csharp" "${COMMON[@]}"
[[ $(count "$T/csharp" .out) -eq 3 ]] && ok "C#: đủ 3 file .out" || bad "C#: thiếu .out"

echo "== Tạo lại output (--regen-out)"
expect_exit 0 "--regen-out" "${BIN[@]}" --regen-out "$T/custom" --solution examples/solutions/custom_sol.cpp --no-zip
[[ $(count "$T/custom" .out) -eq 100 ]] && ok "regen: đủ 100 file .out" || bad "regen: thiếu .out"

echo "== Tham số sai (phải báo lỗi, mã thoát 2)"
expect_exit 2 "--tests 0" "${BIN[@]}" --problem custom --tests 0
expect_exit 2 "min > max" "${BIN[@]}" --problem custom --min 10 --max 1
expect_exit 2 "bounds sai cú pháp" "${BIN[@]}" --problem custom --bounds "1:10,50:x"
expect_exit 2 "tham số không tồn tại" "${BIN[@]}" --problem array --foo 1
expect_exit 2 "đồ thị quá nhiều cạnh" "${BIN[@]}" --problem graph --minN 4 --maxN 4 --minM 7 --maxM 7
expect_exit 0 "sinh DAG liên thông" "${BIN[@]}" --problem graph --tests 12 --seed 11 --directed 1 --dag 1 --connected 1 --minN 8 --maxN 20 --minM 7 --maxM 70 --out "$T/graph_dag" --no-zip
[[ $(count "$T/graph_dag" .inp) -eq 12 ]] && ok "DAG: sinh đủ input đã xác thực" || bad "DAG: số input sai"
expect_exit 0 "sinh đồ thị hai phía liên thông" "${BIN[@]}" --problem graph --tests 12 --seed 12 --bipartite 1 --connected 1 --minN 8 --maxN 20 --minM 7 --maxM 70 --out "$T/graph_bipartite" --no-zip
[[ $(count "$T/graph_bipartite" .inp) -eq 12 ]] && ok "hai phía: sinh đủ input đã xác thực" || bad "hai phía: số input sai"
expect_exit 0 "sinh đồ thị có hướng liên thông mạnh" "${BIN[@]}" --problem graph --tests 4 --seed 13 --directed 1 --stronglyConnected 1 --simple 1 --minN 5 --maxN 5 --minM 5 --maxM 5 --out "$T/graph_strong" --no-zip
[[ $(count "$T/graph_strong" .inp) -eq 4 ]] && ok "liên thông mạnh: sinh đủ input" || bad "liên thông mạnh: số input sai"
expect_exit 0 "sinh DAG liên thông với mật độ thưa" "${BIN[@]}" --problem graph --tests 4 --seed 14 --directed 1 --dag 1 --connected 1 --density 1 --minN 10 --maxN 10 --out "$T/graph_sparse" --no-zip
[[ $(count "$T/graph_sparse" .inp) -eq 4 ]] && awk 'FNR==1 && ($1 != 10 || $2 < 9 || $2 > 30) { bad=1 } END { exit bad }' "$T/graph_sparse"/*.inp && ok "mật độ thưa: sinh đủ DAG liên thông đúng miền số cạnh" || bad "mật độ thưa: thiếu test hoặc sai miền số cạnh"
expect_exit 0 "sinh đồ thị hai phía dày" "${BIN[@]}" --problem graph --tests 4 --seed 15 --bipartite 1 --connected 1 --density 3 --minN 10 --maxN 10 --out "$T/graph_dense" --no-zip
[[ $(count "$T/graph_dense" .inp) -eq 4 ]] && awk 'FNR==1 && ($1 != 10 || $2 < 19 || $2 > 25) { bad=1 } END { exit bad }' "$T/graph_dense"/*.inp && ok "mật độ dày: sinh đủ đồ thị hai phía đúng miền số cạnh" || bad "mật độ dày: thiếu test hoặc sai miền số cạnh"
expect_exit 2 "mật độ cạnh cần đồ thị đơn" "${BIN[@]}" --problem graph --density 1 --simple 0
expect_exit 2 "DAG cần directed" "${BIN[@]}" --problem graph --dag 1
expect_exit 2 "liên thông mạnh không kết hợp DAG" "${BIN[@]}" --problem graph --directed 1 --stronglyConnected 1 --dag 1
expect_exit 2 "dạng bài không tồn tại" "${BIN[@]}" --problem khong_co
expect_exit 2 "seed âm" "${BIN[@]}" --problem custom --seed -5

echo "== Không ghi đè ngoài ý muốn"
"${BIN[@]}" --problem single_n --tests 3 --out "$T/keep" "${COMMON[@]}" > /dev/null
echo "file cua toi" > "$T/keep/ghi_chu.txt"
"${BIN[@]}" --problem single_n --tests 3 --out "$T/keep" "${COMMON[@]}" > /dev/null
[[ -d "$T/keep_2" ]] && ok "lần 2 tự tạo thư mục mới keep_2" || bad "không tạo thư mục mới"
"${BIN[@]}" --problem single_n --tests 2 --out "$T/keep" --force "${COMMON[@]}" > /dev/null
[[ -f "$T/keep/ghi_chu.txt" && $(count "$T/keep" .inp) -eq 2 ]] && ok "--force chỉ xóa .inp/.out, giữ file khác" || bad "--force xóa nhầm file"

echo "== ZIP"
if command -v unzip > /dev/null; then
    unzip -tq "$T/sol_custom.zip" > /dev/null && ok "unzip -t: ZIP hợp lệ" || bad "unzip -t báo lỗi"
    [[ $(unzip -Z1 "$T/sol_custom.zip" | wc -l) -eq 33 ]] && ok "ZIP có 30 input/output cùng 3 file metadata" || bad "ZIP sai số file"
    unzip -Z1 "$T/sol_custom.zip" | grep -qx "SHA256SUMS.txt" && unzip -Z1 "$T/sol_custom.zip" | grep -qx "ojgen_info.txt" && unzip -Z1 "$T/sol_custom.zip" | grep -qx "ojgen_provenance.json" && ok "ZIP kèm manifest, thông tin seed và provenance" || bad "ZIP thiếu metadata"
else
    echo "  (bỏ qua: không có lệnh unzip)"
fi
expect_exit 0 "--zip-dir" "${BIN[@]}" --zip-dir "$T/re"

echo "== Kiểm định phát hành CLI"
expect_exit 0 "--audit chạy validator và so sánh lời giải" "${BIN[@]}" --problem array --tests 5 --seed 2024 --solution examples/solutions/array_sol.cpp --validator "$SOL/array_validator.py" --compare-solution "$SOL/array_alt_sol.py" --audit --no-zip --out "$T/audit"
[[ -f "$T/audit/AUDIT_REPORT.txt" && -f "$T/audit/PACKAGE.txt" && -f "$T/audit/PACKAGE_SHA256SUMS.txt" ]] && ok "audit: lưu báo cáo và manifest gói" || bad "audit: thiếu báo cáo hoặc manifest"
grep -q "Validator hai chiều" "$T/audit/AUDIT_REPORT.txt" && grep -q "Đối chiếu lời giải" "$T/audit/AUDIT_REPORT.txt" && ok "audit: validator hai chiều và đối chiếu lời giải đạt" || bad "audit: thiếu kết quả kiểm tra"
grep -q "Giá trị n đo từ input" "$T/audit/AUDIT_REPORT.txt" && grep -q "Độ tin cậy độ phủ" "$T/audit/AUDIT_REPORT.txt" && grep -q "Phạm vi n: cấu hình nhóm" "$T/audit/ojgen_info.txt" && ok "audit: ghi phép đo, cận nhóm và nhãn tin cậy độ phủ" || bad "audit: thiếu phép đo độ phủ"
expect_exit 0 "audit chạy checker trên từng test" "${BIN[@]}" --problem array --tests 3 --seed 2024 --solution examples/solutions/array_sol.cpp --checker "$SOL/pass_checker.cpp" --checker-args "{input} {output} {answer}" --checker-timeout 5000 --audit --no-zip --out "$T/audit_checker"
grep -q "Checker đạt 3 ca (bao gồm cặp output chính thức và contract" "$T/audit_checker/AUDIT_REPORT.txt" && ok "checker: audit ghi nhận đủ test và timeout" || bad "checker: báo cáo thiếu kết quả"
expect_exit 0 "audit thực thi contract validator và checker" "${BIN[@]}" --problem array --tests 3 --seed 2024 --minN 1 --maxN 10 --minVal 1 --maxVal 100 --solution examples/solutions/array_sol.cpp --validator "$SOL/array_validator.py" --checker "$SOL/pass_checker.cpp" --checker-args "{input} {output} {answer}" --contract-suite tests/fixtures/array-contract.ojcontract.json --audit --no-zip --out "$T/audit_contract"
grep -q "khớp 2 ca hợp đồng" "$T/audit_contract/AUDIT_REPORT.txt" && grep -q "Khớp kết quả mong đợi" "$T/audit_contract/AUDIT_REPORT.txt" && ok "contract: audit ghi nhận ca validator/checker chấp nhận và từ chối" || bad "contract: báo cáo thiếu kết quả suite"
expect_exit 2 "audit chặn checker trả mã lỗi" "${BIN[@]}" --problem array --audit-dir "$T/audit_checker" --no-zip --checker "$SOL/reject_checker.cpp"
grep -q "Checker" "$T/audit_checker/AUDIT_REPORT.txt" && grep -q "Lỗi khi chạy" "$T/audit_checker/AUDIT_REPORT.txt" && ok "checker: lỗi checker được ghi vào báo cáo" || bad "checker: không ghi nhận lỗi"
expect_exit 2 "checker từ chối placeholder không hợp lệ" "${BIN[@]}" --problem array --audit-dir "$T/audit_checker" --no-zip --checker "$SOL/pass_checker.cpp" --checker-args "{input} {bad}"
expect_exit 0 "--audit-dir tạo ZIP sau khi xác nhận cảnh báo" "${BIN[@]}" --audit-dir "$T/audit" --problem array --tests 5 --seed 2024 --solution examples/solutions/array_sol.cpp --allow-audit-warnings
[[ -f "$T/audit.zip" ]] && unzip -Z1 "$T/audit.zip" | grep -qx "PACKAGE_SHA256SUMS.txt" && ok "audit ZIP: có package checksum" || bad "audit ZIP: thiếu gói/manifest"
cp -R "$T/audit" "$T/audit_tampered"
printf '\n999\n' >> "$T/audit_tampered/test01.inp"
expect_exit 2 "--zip-dir từ chối test đã bị sửa" "${BIN[@]}" --zip-dir "$T/audit_tampered"

if [[ -n "${CPP_REF:-}" && -x "$CPP_REF" ]]; then
    echo "== So khớp từng byte với bản C++ gốc ($CPP_REF)"
    i=0
    while IFS= read -r line; do
        i=$((i + 1))
        eval "set -- $line"
        "$CPP_REF" "$@" --out "$T/cmp_c_$i" --no-zip > /dev/null 2>&1
        "${BIN[@]}" "$@" --out "$T/cmp_j_$i" --no-zip > /dev/null 2>&1
        if diff -r -x ojgen_info.txt -x ojgen_provenance.json "$T/cmp_c_$i" "$T/cmp_j_$i" > /dev/null; then ok "giống C++: $line"; else bad "KHÁC C++: $line"; fi
    done <<'CASES'
--problem single_n --tests 20 --seed 3
--problem two_ints --tests 20 --seed 3 --order 1 --minA -50 --maxA 50 --minB -5 --maxB 70
--problem array --tests 20 --seed 3 --distinct 1 --minN 30 --maxN 60 --minVal 1 --maxVal 80 --sorted 1
--problem queries --tests 20 --seed 3 --updatePct 65 --minVal -100 --maxVal 100 --maxN 300 --maxQ 300
--problem string --tests 20 --seed 3 --charset "abc!#" --minLen 5 --maxLen 9
--problem custom --tests 20 --seed 3 --bounds "1:10,50:100,-20:20" --header 1
--problem graph --tests 20 --seed 3 --connected 1 --weighted 1 --minN 5 --maxN 200 --minM 4 --maxM 600
--problem graph --tests 20 --seed 3 --simple 0 --minN 1 --maxN 5 --minM 0 --maxM 30 --weighted 1 --minW -5 --maxW 5
--problem graph --tests 20 --seed 3 --simple 0 --directed 1 --connected 1 --minN 2 --maxN 30 --minM 1 --maxM 80
CASES
fi

echo
echo "Tổng: $PASS đạt, $FAIL thất bại."
[[ $FAIL -eq 0 ]]
