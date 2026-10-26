namespace OjGen.Gui;

/// <summary>Nội dung tab Hướng dẫn.</summary>
internal static class HelpContent
{
    public const string Text =
@"OjGen - TRÌNH TẠO BỘ TEST CHO BÀI LẬP TRÌNH THI ĐẤU

1. QUY TRÌNH NHANH
   a) Chọn 'Dạng bài' và điền tham số ở cột trái (ô sai sẽ viền đỏ, lỗi hiện ở cuối cột).
   b) Nhập số lượng test, seed (để trống = ngẫu nhiên), chọn lời giải chuẩn (.cpp / .py / .exe) nếu muốn tạo cả file .out.
   c) Bấm 'Xem trước test mẫu' để kiểm tra dữ liệu, rồi bấm 'Tạo bộ test'.
   d) Kết quả nằm trong thư mục đầu ra: test01.inp, test01.out, ..., ojgen_info.txt (ghi lại seed và cấu hình) và file .zip cùng tên thư mục.

2. LỜI GIẢI CHUẨN
   - C++: cần trình biên dịch g++ (menu Công cụ → Cài đặt để đổi đường dẫn). Chương trình đọc input từ bàn phím (stdin) và in ra màn hình (stdout).
   - Python: cần cài Python (python / py trên Windows).
   - File .exe đã biên dịch: dùng trực tiếp.
   - Nếu lời giải có freopen(""BAI1.INP""...), hãy bọc trong  #ifndef OJGEN ... #endif  (OjGen tự định nghĩa macro OJGEN khi biên dịch).
   - Lời giải chạy trên máy bạn với quyền của bạn; OjGen chỉ giới hạn thời gian / dung lượng output / bộ nhớ, KHÔNG phải sandbox. Chỉ chạy code bạn tin tưởng.

3. TÁI TẠO BỘ TEST
   Cùng dạng bài + cùng tham số + cùng seed + cùng số test ⇒ luôn ra đúng bộ test cũ (giống hệt bản dòng lệnh C++ trước đây). Seed nằm trong ojgen_info.txt.

4. THƯ MỤC ĐÃ CÓ TEST
   Mặc định OjGen KHÔNG ghi đè: nếu thư mục đã có test thì tạo thư mục mới (_2, _3, ...). Tích 'Ghi đè' để xóa các file .inp/.out/ojgen_info.txt cũ (file khác được giữ nguyên).

5. KẾT QUẢ
   - HOÀN CHỈNH: đủ .inp và .out.   - CHỈ CÓ INPUT: không dùng lời giải.
   - CHƯA HOÀN CHỈNH: lời giải lỗi biên dịch / chạy quá giờ (TLE) / quá dung lượng (OLE) / lỗi khi chạy (RE) ở một số test; các test đó không có file .out.

6. CÔNG CỤ
   - Tạo lại output cho thư mục test có sẵn: chạy lại lời giải trên các file .inp đã có.
   - Nén ZIP thư mục test có sẵn; Kiểm tra nội bộ (selftest): chạy ~240 phép kiểm tra các dạng bài, bộ xác thực, seed và ZIP.

7. THÊM DẠNG BÀI MỚI (dành cho người biết lập trình)
   Tạo một lớp kế thừa Problem trong OjGen.Core/Problems, rồi thêm vào danh sách Problems.All. Giao diện và dòng lệnh sẽ tự có form tham số.
";
}
