# OJGEN 3.0 — Trình tạo test cho lập trình thi đấu

OJGEN 3.0 là công cụ tạo bộ test tự động cho lập trình thi đấu, được chuyển đổi và phát triển từ phiên bản C++ sang **C# (.NET 10)**, đi kèm giao diện đồ họa WPF hiện đại.

Ứng dụng hỗ trợ tạo dữ liệu kiểm thử, chạy lời giải chuẩn để sinh output, quản lý bộ test và đóng gói ZIP. OJGEN cung cấp cả giao diện đồ họa (GUI) và dòng lệnh (CLI), phù hợp với nhu cầu sử dụng trực tiếp lẫn tự động hóa.

## 1. Tính năng chính

- **Giao diện GUI:** Thiết kế WPF với chủ đề Sáng, Tối và Tự động.
- **Dòng lệnh CLI:** Hỗ trợ sử dụng bằng CMD, PowerShell và các script.
- **7 dạng bài:** Single N, Two Integers, Array, Queries, String, Custom và Graph.
- **Tái lập dữ liệu:** Có thể tạo lại bộ test tương ứng bằng cùng seed và cấu hình.
- **Tạo input/output:** Sinh file `.inp` và chạy lời giải chuẩn để tạo file `.out`.
- **Đóng gói ZIP:** Gom các file test để lưu trữ và chia sẻ.
- **Tùy chỉnh cấu hình:** Số lượng test, seed, timeout, giới hạn output, bộ nhớ và các tham số riêng.
- **Tạo lại output:** Chạy lại lời giải trên bộ test có sẵn.
- **Nhật ký và tiến độ:** Theo dõi quá trình chạy, xem thông báo và hủy tác vụ trong GUI.
- **Hỗ trợ nhiều ngôn ngữ:** Chạy lời giải C++, Python và chương trình `.exe`.

## 2. Các phiên bản

### GUI — Giao diện đồ họa
- Ứng dụng Windows có giao diện trực quan.
- Hỗ trợ cấu hình tham số, theo dõi tiến độ và xem trước nội dung test.
- Phù hợp với người dùng muốn thao tác qua cửa sổ ứng dụng.

### CLI — Dòng lệnh
- Sử dụng các lệnh để tạo test, chạy lời giải, tạo lại output và đóng gói ZIP.
- Phù hợp với người dùng quen CMD/PowerShell hoặc muốn tự động hóa quy trình kiểm thử.

Các phiên bản tải xuống được cung cấp trong mục **Releases** của repository.

## 3. Các dạng bài được hỗ trợ

| Dạng bài | Mô tả |
|---|---|
| `single_n` | Bài toán sử dụng tham số số nguyên N |
| `two_ints` | Bài toán sử dụng hai số nguyên |
| `array` | Bài toán liên quan đến mảng |
| `queries` | Bài toán có các truy vấn |
| `string` | Bài toán xử lý chuỗi |
| `custom` | Tạo dữ liệu theo các khoảng giá trị tùy chỉnh |
| `graph` | Bài toán liên quan đến đồ thị |

Với dạng `custom`, người dùng có thể thiết lập nhiều khoảng giá trị, chẳng hạn `1:10,50:100,-20:20`.

## 4. Ví dụ sử dụng CLI

Liệt kê các dạng bài:

```bash
ojgen --list
```

Xem thông tin của một dạng bài:

```bash
ojgen --info array
```

Sinh 20 test với seed cố định:

```bash
ojgen --problem array --tests 20 --seed 2024
```

Sinh test và chạy lời giải chuẩn:

```bash
ojgen --problem array --tests 20 --seed 2024 --solution sol.cpp
```

Sinh test tùy chỉnh:

```bash
ojgen --problem custom --tests 50 --bounds "1:10,50:100,-20:20" --solution sol.py
```

Tạo lại output:

```bash
ojgen --regen-out <thư_mục_test> --solution sol.cpp
```

Đóng gói thư mục test thành ZIP:

```bash
ojgen --zip-dir <thư_mục_test>
```

Chạy tự kiểm tra:

```bash
ojgen --selftest
```

Xem toàn bộ tùy chọn bằng `ojgen --help`.

## 5. Yêu cầu sử dụng

- Hệ điều hành Windows tương thích với phiên bản được tải xuống.
- Nếu chạy lời giải C++, cần trình biên dịch C++ phù hợp, chẳng hạn `g++`.
- Nếu chạy lời giải Python, cần cài Python phù hợp.
- Khả năng chạy mà không cần cài .NET Runtime phụ thuộc vào phiên bản ứng dụng được phát hành.

**Lưu ý:** Việc theo dõi mức sử dụng bộ nhớ không nhất thiết tạo ra giới hạn bộ nhớ cứng trên Windows. Nên kiểm tra bộ test được sinh trước khi sử dụng trong các kỳ thi hoặc hệ thống chấm bài.

## 6. Kiểm thử và trạng thái xác minh

Theo kết quả kiểm thử được ghi nhận trong dự án:

- `ojgen --selftest`: 243 kiểm tra đạt.
- `tests/run_tests.sh`: 65 kiểm tra đầu-cuối đạt.
- `tests/check_wpf_static.py`: kiểm tra tĩnh WPF đạt.
- So sánh 145 cấu hình với phiên bản C++: không có khác biệt về dữ liệu test.

**Lưu ý về GUI:** Kiểm tra tĩnh không thay thế việc chạy thử ứng dụng thực tế trên Windows. Trạng thái xác minh GUI cần được cập nhật sau khi kiểm tra trên môi trường Windows phù hợp.

## 7. Ghi công và giấy phép

OJGEN 3.0 được phát triển dựa trên dự án C++ gốc **[Trinh-tao-contest-Classic-](https://github.com/Namtudifreefire/Trinh-tao-contest-Classic-)**.

- **Tác giả ban đầu:** `minhquackk`.
- **Tác giả/chủ sở hữu dự án nguồn được ghi nhận:** `Namtudifreefire`.
- **Tham khảo giao diện và chủ đề:** [TQLTK](https://github.com/Namtudifreefire/TQLTK).
- **Giấy phép:** MIT — xem file [`LICENSE`](LICENSE) trong repository.

OJGEN 3.0 là phiên bản chuyển đổi và mở rộng từ dự án gốc. Vui lòng giữ nguyên thông tin bản quyền, ghi công tác giả và các thông báo giấy phép liên quan khi phân phối lại.

