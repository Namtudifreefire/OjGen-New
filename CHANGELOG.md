# Changelog

## 3.3.0 — 2026-11-01

- Thu nhỏ input gây sai khác giữa hai lời giải; lưu riêng phản ví dụ gốc và bản rút gọn.
- Audit chạy checker trên mọi test, ghi trạng thái/thời gian từng test và tự thử bằng output lỗi mẫu.
- Gắn nhãn độ phủ cho từng nhóm test và đặt danh sách nhãn bắt buộc trong GUI, kế hoạch JSON, CLI và audit.
- Tiếp tục sinh input sau khi dừng bằng checkpoint có fingerprint cấu hình và SHA-256 từng file; CLI hỗ trợ `--resume`.
- Thêm nhóm test thử thách theo biên, nhiều giá trị trùng và đồ thị liên thông mạnh; bổ sung cấu hình `stronglyConnected`.
- Báo cáo thời gian và working set theo từng test, nêu các test chậm nhất.
- Rà thêm độ phức tạp, cận số, code fence và sample trong statement; đây là gợi ý rà soát, không thay thế review nội dung.
- Nhập test `.in`/`.inp` cùng `.ans`/`.out` từ thư mục hoặc ZIP; so sánh hai bộ test theo tên và SHA-256.
- Thêm hàng đợi chạy tuần tự trong GUI, plugin generator qua DLL, runner Java/C# đơn file, JSON report và script gợi ý CLI.
- Xuất Kattis Problem Package Format 2025-09 release candidate với metadata, statement, test, lời giải và launcher validator.
- Mở rộng bộ hồi quy CLI với kiểm tra checker/audit, kế hoạch nhóm, import và so sánh test; quy trình đóng gói đọc release notes theo phiên bản nguồn.
- Quy trình gói phát hành kèm hướng dẫn plugin; checkpoint runtime được loại khỏi Git.

## 3.2.0 — 2026-10-31

- Audit chạy checker trên từng test với mẫu đối số `{input}`, `{output}`, `{answer}`, `{test}` và timeout riêng; kết quả được ghi vào báo cáo.
- Bổ sung mẫu đồ thị DAG và đồ thị hai phía, kèm kiểm tra cấu hình, chu trình, liên thông, cạnh trùng và hai phía.
- Báo cáo độ phủ đo kích thước thực tế từ input ở các định dạng được nhận diện, so với giới hạn cấu hình và ghi rõ mức tin cậy heuristic.
- Kế hoạch nhóm test có thể lưu/mở JSON; hỗ trợ nhân bản nhóm và đổi thứ tự.
- Thêm quy trình hồi quy, smoke test GUI trên Windows, đóng gói GUI/CLI với checksum và tạo GitHub draft release sau khi toàn bộ bước kiểm tra đạt.

## 3.1.0 — 2026-10-31

- Thêm wizard thiết lập bài, lưu và nạp hồ sơ `.ojproblem.json`, cùng kế hoạch test theo nhóm với seed và cấu hình riêng.
- Thêm nhập test tay/sample, soạn và xem trước statement, kiểm tra tính nhất quán của nội dung đề.
- Thêm báo cáo độ phủ, đối chiếu lời giải trên test nhỏ, khung validator/checker và quy trình kiểm định trước phát hành.
- Thêm manifest SHA-256 cho test và gói; kiểm tra nội dung ZIP sau khi tạo.
- Mở rộng CLI với kiểm định thư mục test, chạy validator tùy chọn, đối chiếu lời giải và xuất báo cáo gói.

## 3.0.0

- Chuyển ứng dụng từ C++ sang C#/.NET 10, bổ sung GUI WPF và giữ CLI dùng chung lõi sinh test.
