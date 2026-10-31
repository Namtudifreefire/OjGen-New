namespace OjGen.Gui;

/// <summary>Nội dung tab Hướng dẫn.</summary>
internal static class HelpContent
{
    public const string Text =
@"OjGen - TRÌNH TẠO BỘ TEST CHO BÀI LẬP TRÌNH THI ĐẤU

1. QUY TRÌNH NHANH
   a) Bấm 'Thiết lập bài' để đặt tên/mã bài, chọn khuôn input, chỉnh giới hạn và gắn lời giải chuẩn. Có thể lưu/mở hồ sơ .ojproblem.json để dùng lại.
   b) Bấm 'Nhóm test' nếu cần chia dữ liệu thành các nhóm như nhỏ, biên, ngẫu nhiên hoặc lớn. Mỗi nhóm có số test, seed và giới hạn riêng; để trống seed nhóm = seed ổn định được tạo từ seed chính.
   c) Bấm 'Test tay/mẫu' để nhập hoặc mở input, chọn cấu hình xác thực, rồi đánh dấu test mẫu nếu cần.
   d) Bấm 'Xem trước test mẫu' để sinh và xác thực input mẫu; khi tạo, rà lại kế hoạch và xác nhận trước khi ghi file.
   e) 'So sánh lời giải' chạy lời giải chuẩn và lời giải cần thử trên test nhỏ; khác biệt có thể lưu thành input và hai output để tái hiện.
   f) Sau khi tạo, nút 'Độ phủ & gợi ý' tóm tắt các kiểu cấu hình/nhóm đã dùng và đề xuất nhóm hoặc chế độ còn thiếu. Đây là gợi ý heuristic, cần đối chiếu với logic đề.
   g) Chọn Tệp → 'Soạn nội dung đề' để viết mô tả, input/output, ràng buộc và ví dụ; xem trước, kiểm tra với cấu hình/test mẫu, lưu bản nháp hoặc xuất Markdown.
   h) Chọn Tệp → 'Đóng gói bài' để kiểm định rồi gom đề, hồ sơ, test và ví dụ thành thư mục/ZIP; có thể kèm validator, checker và lời giải chuẩn (tắt theo mặc định).
   i) Nếu chọn validator, OjGen chạy nó trên mọi input hiện có và các input sai mẫu; nếu chọn lời giải độc lập, OjGen so sánh với lời giải chuẩn trên 100 test nhỏ có seed cố định. Lỗi kiểm định sẽ chặn đóng gói; báo cáo được lưu trong AUDIT_REPORT.txt. Checker được đóng gói nhưng không chạy tự động vì mỗi judge dùng giao diện đối số riêng.
   j) SHA256SUMS.txt ghi dấu từng file .inp/.out. Kiểm định sẽ báo lỗi nếu file đã đổi sau lần sinh; gói bài tạo thêm PACKAGE_SHA256SUMS.txt, AUDIT_REPORT.txt và PACKAGE.txt (seed, phiên bản/runtime/OS, SHA-256 các tool/lời giải).
   k) Chọn Công cụ → 'Khung validator/checker' để tạo file C++/Python mẫu cùng README hướng dẫn các TODO cần hoàn thiện.
   l) Kết quả sinh test nằm trong thư mục đầu ra: test01.inp, test01.out, ..., ojgen_info.txt (ghi lại bài, seed, test nhập tay và cấu hình từng nhóm), SHA256SUMS.txt và file .zip cùng tên thư mục.

2. LỜI GIẢI CHUẨN
   - C++: cần trình biên dịch g++ (menu Công cụ → Cài đặt để đổi đường dẫn). Chương trình đọc input từ bàn phím (stdin) và in ra màn hình (stdout).
   - Python: cần cài Python (python / py trên Windows).
   - File .exe đã biên dịch: dùng trực tiếp.
   - Nếu lời giải có freopen(""BAI1.INP""...), hãy bọc trong  #ifndef OJGEN ... #endif  (OjGen tự định nghĩa macro OJGEN khi biên dịch).
   - Lời giải chuẩn, validator và lời giải dùng để đối chiếu chạy trên máy bạn với quyền của bạn; OjGen chỉ giới hạn thời gian / dung lượng output / bộ nhớ, KHÔNG phải sandbox. Chỉ chạy code bạn tin tưởng.

3. TÁI TẠO BỘ TEST
   Cùng dạng bài + cùng tham số + cùng seed + cùng số test ⇒ luôn ra đúng bộ test cũ (giống hệt bản dòng lệnh C++ trước đây). Seed nằm trong ojgen_info.txt.

4. THƯ MỤC ĐÃ CÓ TEST
   Mặc định OjGen KHÔNG ghi đè: nếu thư mục đã có test thì tạo thư mục mới (_2, _3, ...). Tích 'Ghi đè' để xóa các file .inp/.out, ojgen_info.txt và SHA256SUMS.txt cũ (file khác được giữ nguyên).

5. KẾT QUẢ
   - HOÀN CHỈNH: đủ .inp và .out.   - CHỈ CÓ INPUT: không dùng lời giải.
   - CHƯA HOÀN CHỈNH: lời giải lỗi biên dịch / chạy quá giờ (TLE) / quá dung lượng (OLE) / lỗi khi chạy (RE) ở một số test; các test đó không có file .out.

6. CÔNG CỤ
   - Tạo lại output cho thư mục test có sẵn: chạy lại lời giải trên các file .inp đã có.
   - Nén ZIP thư mục test có sẵn; Kiểm tra nội bộ (selftest): chạy ~240 phép kiểm tra các dạng bài, bộ xác thực, seed và ZIP.

7. SOẠN VÀ ĐÓNG GÓI ĐỀ
   - Bản nháp đề được lưu cạnh hồ sơ, có phần mô tả, input, output, ràng buộc và ví dụ; Markdown hỗ trợ tiêu đề, danh sách, code và inline code.
   - Kiểm tra tự động phát hiện thiếu mục, giới hạn không hợp lệ, thiếu output ví dụ và khác biệt giữa output mẫu khai báo với output lời giải chuẩn. Các cảnh báo cần được đối chiếu thủ công với phát biểu toán học của đề.
   - Gói bài có statement.md, statement.html, problem.ojproblem.json, AUDIT_REPORT.txt, PACKAGE_SHA256SUMS.txt, thư mục tests/, samples/, và các tool tùy chọn; ZIP có thể gửi hoặc lưu trữ.
   - Validator/checker sinh ra là khung ban đầu, không thay cho việc cài đặt điều kiện hợp lệ hoặc quy tắc chấm của bài.

8. THÊM DẠNG BÀI MỚI (dành cho người biết lập trình)
   Tạo một lớp kế thừa Problem trong OjGen.Core/Problems, rồi thêm vào danh sách Problems.All. Giao diện và dòng lệnh sẽ tự có form tham số.
";
}
