# OjGen 3.2.0

**Ngày phát hành:** 31/10/2026  
**Nền tảng:** Windows x64

OjGen là công cụ tạo và kiểm định bộ test cho các bài lập trình. Bản 3.2.0 bổ sung kiểm tra checker, ràng buộc đồ thị và khả năng lưu kế hoạch test để dùng lại.

## Tính năng mới

- **Chạy checker trong quá trình kiểm định:** chạy checker trên từng test, đặt mẫu đối số và timeout riêng; kết quả được ghi vào `AUDIT_REPORT.txt`.
- **Thêm ràng buộc đồ thị:** hỗ trợ sinh DAG và đồ thị hai phía, kết hợp với các tùy chọn đồ thị liên thông, đơn, có hướng và có trọng số.
- **Đo độ phủ bộ test:** báo cáo kích thước thực tế đọc được từ input và so sánh với giới hạn cấu hình. Các gợi ý phân loại trường hợp vẫn mang tính heuristic, nên hãy rà soát đề và test trước khi phát hành.
- **Lưu kế hoạch test:** lưu và mở kế hoạch `.ojplan.json`; có thể nhân bản, đổi tên và sắp xếp lại các nhóm test.
- **Cải thiện quy trình kiểm tra và đóng gói:** bổ sung hồi quy, smoke test GUI trên Windows, gói ZIP có cả GUI và CLI cùng checksum.

## Tải và chạy

Tải `OjGen-3.2.0-win-x64.zip` trong phần **Assets** của bản phát hành rồi giải nén. Chọn chương trình phù hợp:

- `GUI/OjGen.exe` — giao diện đồ họa.
- `CLI/ojgen.exe` — dòng lệnh.

Hai chương trình được đóng gói dạng self-contained cho Windows x64, không cần cài riêng .NET Runtime.

## Chạy checker bằng CLI

Ví dụ kiểm định một bộ test đã tạo:

```powershell
ojgen --audit-dir .\output\problem --problem graph --checker .\checker.exe `
  --checker-args "{input} {output} {answer}" --checker-timeout 5000 --no-zip
```

Các placeholder được hỗ trợ: `{input}`, `{output}`, `{answer}` và `{test}`. Theo mẫu trên, `{output}` và `{answer}` cùng trỏ tới file `.out` chính thức. Nếu checker của bạn nhận đối số theo thứ tự khác, hãy chỉnh `--checker-args` cho phù hợp.
