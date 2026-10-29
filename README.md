# OJGEN 3.0 — Trình tạo test cho lập trình thi đấu

OJGEN 3.0 là công cụ tạo bộ test tự động cho lập trình thi đấu, được chuyển đổi và phát triển từ phiên bản C++ sang **C# (.NET 10)**, đi kèm giao diện đồ họa WPF hiện đại.

Ứng dụng hỗ trợ tạo dữ liệu kiểm thử, chạy lời giải chuẩn để sinh output, quản lý bộ test và đóng gói ZIP. OJGEN cung cấp cả giao diện đồ họa (GUI) và dòng lệnh (CLI), phù hợp với nhu cầu sử dụng trực tiếp lẫn tự động hóa.

## 1. Tính năng chính

- **Giao diện GUI:** Thiết kế WPF với chủ đề Sáng, Tối và Tự động.
- **Dòng lệnh CLI:** Hỗ trợ sử dụng bằng CMD, PowerShell và các script.
- **14 cấu trúc input:** số nguyên, hai số, mảng/hoán vị, nhiều bộ test, hai mảng/ma trận, xâu, lưới ký tự, cặp/đoạn, truy vấn, đồ thị, cây và tùy chỉnh.
- **Danh mục chủ đề VNOI và Codeforces:** chọn tag thuật toán để OjGen gợi ý khuôn input gần nhất; tag Codeforces có liên kết tới một bài mẫu.
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

OjGen chia bộ chọn thành **dạng chính → nhánh con** theo cấu trúc input. Các tuỳ chọn trong từng nhánh tiếp tục mô tả biến thể dữ liệu.

| Dạng chính | Nhánh con | Biến thể hiện có |
|---|---|---|
| Số và bộ giá trị | `single_n`, `two_ints` | Một số; cặp số có thể ràng buộc thứ tự |
| Dãy và truy vấn | `array`, `queries`, `prefix_sum_range`, `prefix_subsequence_range` | Mảng ngẫu nhiên/hoán vị/tăng/giảm; cập nhật/truy vấn đoạn; input QBOJ `sumarr`; input QBOJ TDC với truy vấn tổng dãy con bằng tiền tố các phần tử dương |
| Xâu và ký tự | `string` | Charset chữ, số, nhị phân, DNA hoặc tập ký tự tự chọn; độ dài cố định/ngẫu nhiên |
| Ma trận và lưới | `matrix`, `grid` | Ma trận số, hai mảng song song, lưới ký tự có thể đặt `S` và `T` |
| Danh sách cặp và đoạn | `pairs` | Cặp số tổng quát hoặc đoạn `[l, r]` |
| Đồ thị và cây | `graph`, `tree` | Đồ thị có hướng/vô hướng, trọng số; cây đường thẳng, hình sao, nhị phân hoặc ngẫu nhiên |
| Nhiều bộ test | `multi_test` | Mỗi bộ con là dãy số hoặc xâu |
| Dữ liệu tùy chỉnh | `custom` | Khoảng giá trị chung hoặc giới hạn riêng theo vị trí |

Các tiêu đề QBOJ như `greedy`, `prefixsum`, `slidwin`, `two point`, `backtr` và `dynamic` là **chủ đề/thuật toán**. Chúng không chỉ ra duy nhất cấu trúc input, nên được dùng để gợi ý nhánh dữ liệu sau khi đọc đề, không thay cho dạng chính.

GUI có bộ chọn tag thuật toán từ [VNOI](https://oj.vnoi.info/tags/) và [problemset Codeforces](https://codeforces.com/api/problemset.problems) (đồ thị, cấu trúc dữ liệu, xâu, quy hoạch động, toán, hình học, tham lam, ...). Chọn tag sẽ chuyển sang khuôn input phù hợp nhất hiện có; với tag Codeforces, giao diện cũng hiện liên kết tới một bài mẫu công khai. Đối chiếu định dạng input và giới hạn của đề mẫu trước khi sinh test: tag không xác định duy nhất input hay lời giải. Để tạo file output, hãy chọn lời giải chuẩn tương ứng trong ô **Lời giải chuẩn**.

| Dạng bài | Mô tả |
|---|---|
| `single_n` | Bài toán sử dụng tham số số nguyên N |
| `two_ints` | Bài toán sử dụng hai số nguyên |
| `array` | Bài toán liên quan đến mảng |
| `queries` | Bài toán có các truy vấn |
| `prefix_sum_range` | Mảng và truy vấn tổng đoạn theo khuôn QBOJ `sumarr` |
| `prefix_subsequence_range` | Khuôn truy vấn tiền tố theo bài QBOJ `Tổng Dãy Con` (TDC) |
| `string` | Bài toán xử lý chuỗi |
| `matrix` | Ma trận số hoặc hai mảng cùng độ dài |
| `multi_test` | Nhiều bộ test, mỗi bộ là mảng số hoặc xâu |
| `grid` | Lưới ký tự, tùy chọn ô bắt đầu `S` và đích `T` |
| `pairs` | Danh sách cặp số hoặc đoạn `[l, r]` |
| `tree` | Cây dạng danh sách cạnh hoặc danh sách cha, có thể có trọng số |
| `custom` | Tạo dữ liệu theo các khoảng giá trị tùy chỉnh |
| `graph` | Bài toán liên quan đến đồ thị |

Với dạng `custom`, người dùng có thể thiết lập nhiều khoảng giá trị, chẳng hạn `1:10,50:100,-20:20`.
Với `array`, có thể tạo hoán vị `1..n`; `matrix` hỗ trợ cả dạng hai mảng trên hai dòng.

Trang QBOJ dùng các nhãn `backtr`, `basic`, `binary`, `chaotic`, `Chưa phân loại`, `codeendcode`, `doublearr`, `dynamic`, `Estinum`, `greedy`, `Hash`, `map`, `mark`, `markstr`, `math`, `normal`, `Numerals`, `numstr`, `prefixcount`, `prefixmaxmin`, `prefixsum`, `Recipe`, `rule`, `slidwin`, `Sort`, `special num`, `subarr`, `subarr1`, `two point` và `word` để mô tả chủ đề. Những nhãn này không quy định một khuôn input duy nhất; OjGen chọn mẫu theo cấu trúc dữ liệu để dùng được cho nhiều bài trong cùng nhóm.

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
- **Tác giả/chủ sở hữu dự án nguồn được ghi nhận:** `Namtudifreefire/thienthuongthienha_duyngadocton_`.
- **Tham khảo giao diện và chủ đề:** [TQLTK](https://github.com/Namtudifreefire/TQLTK).
- **Giấy phép:** MIT — xem file [`LICENSE`](LICENSE) trong repository.

OJGEN 3.0 là phiên bản chuyển đổi và mở rộng từ dự án gốc. Vui lòng giữ nguyên thông tin bản quyền, ghi công tác giả và các thông báo giấy phép liên quan khi phân phối lại.

