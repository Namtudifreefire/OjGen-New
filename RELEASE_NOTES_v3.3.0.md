# OjGen 3.3.0

OjGen 3.3.0 bổ sung công cụ để tạo, kiểm định, tái sử dụng và so sánh bộ test thuận tiện hơn trên GUI và CLI.

## Điểm mới

- Kiểm thử đối chiếu có thể thu nhỏ input gây sai khác; lưu cả phản ví dụ gốc và bản đã thu nhỏ.
- Shrinker cấu trúc hỗ trợ cây, ma trận, lưới, truy vấn và nhiều bộ test; mỗi ứng viên được xác thực theo khuôn trước khi chạy lại.
- Đồ thị có thể chọn mật độ thưa/vừa/dày theo sức chứa hợp lệ trong khi vẫn áp dụng các cờ liên thông, DAG, hai phía và đồ thị đơn.
- Audit chạy checker trên mọi test, ghi trạng thái/thời gian từng test và tự thử với output lỗi mẫu.
- Thêm contract suite `.ojcontract.json` để khai báo ca validator/checker cần chấp nhận hoặc từ chối; dùng trong GUI, audit và CLI.
- Nhãn độ phủ cho từng nhóm test và danh sách nhãn bắt buộc dùng trong kế hoạch, CLI và audit.
- GUI và CLI `--resume` tiếp tục từ checkpoint input/output có fingerprint; chỉ bỏ qua output đã khớp input, lời giải, cấu hình chạy và SHA-256.
- Thêm nhóm test gợi ý cho giá trị nhỏ/lớn, dữ liệu trùng và đồ thị liên thông mạnh.
- Báo cáo thời gian và working set xấp xỉ theo từng test, kèm danh sách test chậm.
- Kiểm tra statement để nhắc về cận, độ phức tạp, code fence và sự khớp với sample.
- Nhập test `.in`/`.inp` và `.ans`/`.out` từ thư mục hoặc ZIP; so sánh hai bộ theo tên và SHA-256.
- Hàng đợi cấu hình chạy tuần tự trong GUI.
- Plugin generator qua DLL; runner Java source và C# đơn file; CLI xuất JSON và tạo completion cho Bash/PowerShell.
- Xuất Kattis Problem Package Format 2025-09 release candidate.
- Lưu toàn bộ phiên soạn đề vào `.ojproject`, có autosave và khôi phục; project gồm cấu hình, statement, nhóm, sample/test tay, lời giải và các tùy chọn audit/đóng gói.
- Thêm provenance JSON và inventory CSV theo từng test; báo min/max theo cấu hình và phạm vi thực đo cho n/m/q/t, kèm gợi ý cận chưa chạm. Phép đo số học không khẳng định đã phủ đủ logic bài.
- Kiểm tra cấu trúc/checksum gói OjGen và Kattis trước khi ZIP; lưu dự án GUI bằng menu hoặc Ctrl+S/Ctrl+Shift+S/Ctrl+O.
- Release preflight xác minh release notes, phiên bản GUI/CLI, layout ZIP và checksum manifests.
- Gói phát hành kèm hướng dẫn plugin và ghi chú phát hành theo phiên bản nguồn.
- Kiểm kê bộ test cũ trong GUI: lọc/đổi tên/nhóm hàng loạt, phát hiện input trùng, output thiếu, xem cận số, xuất CSV và trực quan đồ thị/cây/lưới/phân phối.
- Chạy lại kiểm định input hoặc checker trên bộ test có sẵn mà không tạo lại input/output; ma trận nhiều lời giải báo test phân biệt với lời giải chuẩn.
- Kế hoạch nhóm có điểm và quan hệ tiên quyết, dùng làm metadata judge; package OjGen ghi lại kế hoạch.
- Thêm khung checker Testlib, sai số số thực, token không thứ tự; validator theo khuôn có contract suite và nút chạy thử.
- Mở rộng statement preview với bảng, ảnh HTTPS, văn bản đa ngôn ngữ và một tập công thức TeX cơ bản.
- Run profile `.ojrun.json` hỗ trợ build/run nhiều file; runner thêm C, Rust, Go, Pascal, Kotlin. Hàng đợi GUI lưu bền và cho sửa seed, output, nhóm, thứ tự để chạy lại lỗi.
- Benchmark theo nhóm ghi median, p95, working set đỉnh và môi trường chạy.

## Lưu ý tương thích

Định dạng Kattis 2025-09 là release candidate và hiện chưa được hỗ trợ rộng. Hãy kiểm tra judge đích trước khi nhập gói. Lời giải chuẩn phải là mã nguồn; validator Kattis xuất gói cần là Python độc lập. Gói vẫn cần được rà nội dung và metadata trước khi sử dụng.

Các phép đo độ phủ và gợi ý kiểm tra statement mang tính heuristic. Runner C# cần .NET SDK 10; runner Java source launcher cần Java 11 trở lên trong `PATH`.
