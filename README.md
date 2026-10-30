# OJGEN 3.0 — Trình tạo test cho lập trình thi đấu

OJGEN 3.0 là công cụ tạo bộ test tự động cho lập trình thi đấu, được chuyển đổi và phát triển từ phiên bản C++ sang **C# (.NET 10)**, đi kèm giao diện đồ họa WPF hiện đại.

Ứng dụng hỗ trợ tạo dữ liệu kiểm thử, chạy lời giải chuẩn để sinh output, quản lý bộ test và đóng gói ZIP. OJGEN cung cấp cả giao diện đồ họa (GUI) và dòng lệnh (CLI), phù hợp với nhu cầu sử dụng trực tiếp lẫn tự động hóa.

## 1. Tính năng chính

- **Giao diện GUI:** Thiết kế WPF với chủ đề Sáng, Tối và Tự động.
- **Dòng lệnh CLI:** Hỗ trợ sử dụng bằng CMD, PowerShell và các script.
- **19 cấu trúc input:** số nguyên, hai số, mảng/hoán vị, nhiều bộ test, hai mảng/ma trận, xâu, lưới ký tự, cặp/đoạn, truy vấn, đồ thị, cây, tùy chỉnh và các khuôn cờ vua.
- **Danh mục chủ đề QBOJ, VNOI, Codeforces và LQDOJ:** chọn tag thuật toán trong GUI hoặc CLI để OjGen gợi ý khuôn input gần nhất; xem bài mẫu từ giao diện hoặc dòng lệnh.
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
| Dữ liệu tùy chỉnh | `custom`, `chess_moves`, `chess_piece_moves`, `chess_knight_targets`, `chess_knight_reachability`, `chess_knight_shortest` | Khoảng số chung/riêng; đếm hoặc liệt kê nước đi các quân cờ; kiểm tra và tìm đường đi của quân Mã trên bàn 8×8 |

Các tiêu đề QBOJ như `greedy`, `prefixsum`, `slidwin`, `two point`, `backtr` và `dynamic` là **chủ đề/thuật toán**. Chúng không chỉ ra duy nhất cấu trúc input, nên được dùng để gợi ý nhánh dữ liệu sau khi đọc đề, không thay cho dạng chính.

GUI có bộ chọn tag thuật toán từ [danh sách bài QBOJ](http://qboj.zapto.org/problems/), [VNOI](https://oj.vnoi.info/tags/), [problemset Codeforces](https://codeforces.com/api/problemset.problems) và [LQDOJ](https://lqdoj.edu.vn/problems/). Danh mục LQDOJ được lấy từ bộ lọc “Dạng bài” của trang danh sách: tại ngày 30-10-2026 có 163 nhãn, trong đó 156 nhãn gắn với ít nhất một bài; mỗi nhãn trong OjGen có một bài mẫu để mở trực tiếp. Các chủ đề bao gồm đồ thị, cấu trúc dữ liệu, xâu, quy hoạch động, toán, hình học, tham lam và các kỹ thuật khác. Chọn tag sẽ chuyển sang khuôn input gần nhất hiện có. Đối chiếu định dạng input và giới hạn của đề mẫu trước khi sinh test: tag không xác định duy nhất input hay lời giải. Để tạo file output, hãy chọn lời giải chuẩn tương ứng trong ô **Lời giải chuẩn**.

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
| `chess_moves` | Tọa độ `x y` của quân Mã trên bàn 8×8; output là số ô có thể đi |
| `chess_piece_moves` | Chọn quân Mã, Vua, Xe, Tượng hoặc Hậu; input tọa độ `x y`, output số ô đi được trên bàn cờ trống |
| `chess_knight_targets` | Tọa độ `x y` của quân Mã; output số ô và danh sách tọa độ đích theo thứ tự tăng dần |
| `chess_knight_reachability` | Hai tọa độ `x1 y1 x2 y2`; kiểm tra Mã có thể đi tới đích trong một nước |
| `chess_knight_shortest` | Hai tọa độ `x1 y1 x2 y2`; tìm số nước Mã ít nhất trên bàn cờ trống |
| `graph` | Bài toán liên quan đến đồ thị |

Với dạng `custom`, người dùng có thể thiết lập nhiều khoảng giá trị, chẳng hạn `1:10,50:100,-20:20`.
Với dạng `chess_moves`, input là một cặp tọa độ `x y`; chọn vị trí ngẫu nhiên, ở góc, ở cạnh hoặc vùng trung tâm. Lời giải chuẩn đã chọn sẽ tạo output là số nước đi hợp lệ.
Các khuôn `chess_piece_moves`, `chess_knight_targets`, `chess_knight_reachability` và `chess_knight_shortest` bổ sung các bài cùng chủ đề. Chúng giả sử bàn cờ trống, không có quân cản; khuôn đếm quân cờ cho phép chọn Mã, Vua, Xe, Tượng hoặc Hậu.
Với `array`, có thể tạo hoán vị `1..n`; `matrix` hỗ trợ cả dạng hai mảng trên hai dòng.

Trang QBOJ có các nhãn `backtr`, `basic`, `binary`, `chaotic`, `Chưa phân loại`, `codeendcode`, `doublearr`, `dynamic`, `Estinum`, `greedy`, `Hash`, `map`, `mark`, `markstr`, `math`, `normal`, `Numerals`, `numstr`, `prefixcount`, `prefixmaxmin`, `prefixsum`, `Recipe`, `rule`, `slidwin`, `Sort`, `special num`, `subarr`, `subarr1`, `two point` và `word`. OjGen đưa các nhãn có ý nghĩa chủ đề vào bộ chọn và loại `Chưa phân loại`; tại thời điểm đọc, `Hash` và `normal` không gắn với bài nào trong 1.069 dòng danh sách. Những nhãn này không quy định một khuôn input duy nhất; OjGen chọn mẫu theo cấu trúc dữ liệu để dùng được cho nhiều bài trong cùng nhóm.

## 4. Ví dụ sử dụng CLI

Liệt kê các dạng bài và chủ đề thuật toán:

```bash
ojgen --list
ojgen --topics
ojgen --topics --topic-source LQDOJ
```

Xem một chủ đề, bài mẫu và khuôn input được gợi ý:

```bash
ojgen --topic "LQDOJ:greedy (tham lam)"
```

Sinh test từ khuôn được gợi ý bởi chủ đề (có thể chỉ rõ `--problem` để chọn khuôn khác):

```bash
ojgen --topic "LQDOJ:greedy (tham lam)" --tests 20 --seed 2024
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

