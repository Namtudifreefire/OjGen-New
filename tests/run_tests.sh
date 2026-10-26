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

T=output/_tests
rm -rf "$T"
mkdir -p "$T"
LOG="$T/last.log"
COMMON=(--no-zip)
SOL=tests/solutions

echo "== Tự kiểm tra nội bộ"
expect_exit 0 "--selftest" "${BIN[@]}" --selftest

echo "== Lệnh ví dụ"
expect_exit 0 "--list" "${BIN[@]}" --list
expect_exit 0 "--info graph" "${BIN[@]}" --info graph
expect_exit 0 "--problem array --tests 20 --seed 2024" "${BIN[@]}" --problem array --tests 20 --seed 2024 --out "$T/array"
[[ $(count "$T/array" .inp) -eq 20 && -f "$T/array.zip" ]] && ok "array: 20 file .inp + ZIP" || bad "array: thiếu file"
expect_exit 0 "--problem custom --tests 100 --seed 2024" "${BIN[@]}" --problem custom --tests 100 --seed 2024 --out "$T/custom"
[[ $(count "$T/custom" .inp) -eq 100 ]] && ok "custom: 100 file .inp" || bad "custom: số file sai"

echo "== Tái tạo bằng seed"
"${BIN[@]}" --problem custom --tests 100 --seed 2024 --out "$T/custom_again" "${COMMON[@]}" > /dev/null
diff -r -x ojgen_info.txt -x '*.zip' "$T/custom" "$T/custom_again" > /dev/null && ok "cùng seed -> cùng bộ test" || bad "cùng seed nhưng khác dữ liệu"

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

echo "== Tạo lại output (--regen-out)"
expect_exit 0 "--regen-out" "${BIN[@]}" --regen-out "$T/custom" --solution examples/solutions/custom_sol.cpp --no-zip
[[ $(count "$T/custom" .out) -eq 100 ]] && ok "regen: đủ 100 file .out" || bad "regen: thiếu .out"

echo "== Tham số sai (phải báo lỗi, mã thoát 2)"
expect_exit 2 "--tests 0" "${BIN[@]}" --problem custom --tests 0
expect_exit 2 "min > max" "${BIN[@]}" --problem custom --min 10 --max 1
expect_exit 2 "bounds sai cú pháp" "${BIN[@]}" --problem custom --bounds "1:10,50:x"
expect_exit 2 "tham số không tồn tại" "${BIN[@]}" --problem array --foo 1
expect_exit 2 "đồ thị quá nhiều cạnh" "${BIN[@]}" --problem graph --minN 4 --maxN 4 --minM 7 --maxM 7
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
    [[ $(unzip -Z1 "$T/sol_custom.zip" | wc -l) -eq 30 ]] && ok "ZIP có đúng 30 file" || bad "ZIP sai số file"
else
    echo "  (bỏ qua: không có lệnh unzip)"
fi
expect_exit 0 "--zip-dir" "${BIN[@]}" --zip-dir "$T/re"

if [[ -n "${CPP_REF:-}" && -x "$CPP_REF" ]]; then
    echo "== So khớp từng byte với bản C++ gốc ($CPP_REF)"
    i=0
    while IFS= read -r line; do
        i=$((i + 1))
        eval "set -- $line"
        "$CPP_REF" "$@" --out "$T/cmp_c_$i" --no-zip > /dev/null 2>&1
        "${BIN[@]}" "$@" --out "$T/cmp_j_$i" --no-zip > /dev/null 2>&1
        if diff -r -x ojgen_info.txt "$T/cmp_c_$i" "$T/cmp_j_$i" > /dev/null; then ok "giống C++: $line"; else bad "KHÁC C++: $line"; fi
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
