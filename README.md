# OJGEN 3.0 — Trình tạo test cho bài lập trình thi đấu (bản C#)

Bản chuyển từ C++ sang **C# (.NET 10)** của công cụ OJGEN (gốc: *Trinh-tao-contest-Classic-*), kèm **giao diện WPF** theo phong cách ứng dụng **TQLTK**.

> Trạng thái kiểm chứng: phần lõi (`OjGen.Core`) và dòng lệnh (`ojgen`) đã được build và kiểm thử đầy đủ trên Linux.
> **Dự án giao diện WPF (`OjGen.App`) chưa được biên dịch/chạy thử** vì môi trường tạo mã không có Windows Desktop SDK. Hãy build trên Windows (mục 2) và gửi lại thông báo lỗi nếu có.

## 1. Cấu trúc

| Thư mục | Nội dung |
|---|---|
| `src/OjGen.Core` | Thư viện lõi: 7 dạng bài, bộ sinh số MT19937-64, chạy lời giải, tạo ZIP, tự kiểm tra |
| `src/OjGen.Cli` | Chương trình dòng lệnh `ojgen` |
| `src/OjGen.App` | Giao diện WPF `OjGen.exe` (giao diện Sáng/Tối/Tự động) |
| `examples/solutions` | Lời giải mẫu cho từng dạng bài |
| `tests` | `run_tests.sh` (kiểm thử đầu-cuối), lời giải mẫu lỗi, `check_wpf_static.py` |

## 2. Build (Windows)

Yêu cầu: **.NET 10 SDK**; muốn tạo output từ lời giải C++ thì cần `g++` (MSYS2 MinGW-w64); lời giải Python cần `python`.

```
build.bat      :: build Release cả 3 dự án
publish.bat    :: xuất OjGen.exe (1 file, không cần cài .NET) vào publish\gui, ojgen.exe vào publish\cli
```

Hoặc: `dotnet run --project src/OjGen.App`.

## 3. Dạng bài

`single_n`, `two_ints`, `array`, `queries`, `string`, `custom` (nhiều khoảng giá trị, nhập mỗi dòng một khoảng, ví dụ `1:10`), `graph`.
Xem định dạng và tham số: `ojgen --info <dạng>`.

## 4. Dòng lệnh

```
ojgen --list
ojgen --problem array --tests 20 --seed 2024 --solution examples/solutions/array_sol.cpp
ojgen --problem custom --tests 50 --bounds "1:10,50:100,-20:20" --solution sol.py
ojgen --regen-out <thư mục test> --solution sol.cpp
ojgen --zip-dir <thư mục test>
ojgen --selftest
```
Mã thoát: 0 = thành công, 1 = lời giải lỗi ở một số test, 2 = sai tham số. Chi tiết: `ojgen --help`.

## 5. Khác biệt so với bản C++ (cần bạn xác nhận)

Do yêu cầu "cải tiến… cần thêm một số thay đổi phù hợp" chưa nêu cụ thể, tôi đã chọn:

1. Giao diện WPF (Sáng/Tối/Tự động theo hệ thống, như TQLTK); biểu mẫu tham số tự dựng theo từng dạng bài.
2. Chạy bất đồng bộ, có nút Hủy, thanh tiến độ, tab Nhật ký; xem trước nội dung test.
3. Hỗ trợ lời giải **Python** (`.py`) ngoài `.cpp`/`.exe`; kéo-thả file lời giải.
4. ZIP dùng thư viện chuẩn của .NET (bản C++ tự cài đặt); giới hạn bộ nhớ lời giải kiểm tra bằng cách thăm dò mức dùng đỉnh (không phải giới hạn cứng trên Windows).
5. Cấu hình lưu tại `%AppData%\OjGen\ojgen_settings.ini`; thư mục output mặc định `Documents\OjGen\output`.
6. Bản console tương tác (menu) được thay bằng GUI + CLI.

Hành vi sinh dữ liệu giữ nguyên: **cùng seed + cùng cấu hình cho file test giống hệt bản C++** (đã so sánh 145 cấu hình, 0 khác biệt).

## 6. Kiểm thử đã chạy

- `ojgen --selftest`: 243 kiểm tra đạt.
- `tests/run_tests.sh`: 65 kiểm tra đầu-cuối đạt (đặt `CPP_REF` để so byte với bản C++).
- `python3 tests/check_wpf_static.py`: kiểm tra tĩnh XAML/code-behind (tên control, XML hợp lệ, tài nguyên) đạt; code-behind WPF biên dịch được với bộ stub (chỉ kiểm tra logic, **không** thay thế biên dịch thật).

## 7. Ghi công & giấy phép

Mã gốc C++: dự án *Trinh-tao-contest-Classic-* (Namtudifreefire; tác giả ban đầu minhquackk). Giao diện/chủ đề: theo *TQLTK*. Giấy phép **MIT** (xem `LICENSE`).
