# OjGen 3.3.0

OjGen 3.3.0 bổ sung công cụ để tạo, kiểm định, tái sử dụng và so sánh bộ test thuận tiện hơn trên GUI và CLI.

## Điểm mới

- Kiểm thử đối chiếu có thể thu nhỏ input gây sai khác; lưu cả phản ví dụ gốc và bản đã thu nhỏ.
- Audit chạy checker trên mọi test, ghi trạng thái/thời gian từng test và tự thử với output lỗi mẫu.
- Nhãn độ phủ cho từng nhóm test và danh sách nhãn bắt buộc dùng trong kế hoạch, CLI và audit.
- CLI `--resume` tiếp tục sinh input từ checkpoint có fingerprint cấu hình và SHA-256 từng file; file lạ hoặc chỉnh sửa sẽ chặn khôi phục.
- Thêm nhóm test gợi ý cho giá trị nhỏ/lớn, dữ liệu trùng và đồ thị liên thông mạnh.
- Báo cáo thời gian và working set xấp xỉ theo từng test, kèm danh sách test chậm.
- Kiểm tra statement để nhắc về cận, độ phức tạp, code fence và sự khớp với sample.
- Nhập test `.in`/`.inp` và `.ans`/`.out` từ thư mục hoặc ZIP; so sánh hai bộ theo tên và SHA-256.
- Hàng đợi cấu hình chạy tuần tự trong GUI.
- Plugin generator qua DLL; runner Java source và C# đơn file; CLI xuất JSON và tạo completion cho Bash/PowerShell.
- Xuất Kattis Problem Package Format 2025-09 release candidate.
- Gói phát hành kèm hướng dẫn plugin và ghi chú phát hành theo phiên bản nguồn.

## Lưu ý tương thích

Định dạng Kattis 2025-09 là release candidate và hiện chưa được hỗ trợ rộng. Hãy kiểm tra judge đích trước khi nhập gói. Lời giải chuẩn phải là mã nguồn; validator Kattis xuất gói cần là Python độc lập. Gói vẫn cần được rà nội dung và metadata trước khi sử dụng.

Các phép đo độ phủ và gợi ý kiểm tra statement mang tính heuristic. Runner C# cần .NET SDK 10; runner Java source launcher cần Java 11 trở lên trong `PATH`.
