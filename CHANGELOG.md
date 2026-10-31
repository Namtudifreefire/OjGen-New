# Changelog

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
