# Changelog

## 3.3.2 — 2026-11-15

- **Plugin API hoàn thiện**: Triển khai đầy đủ hệ thống mở rộng plugin với 6 điểm mở rộng (Validator, Checker, Exporter, SolutionRunner, EventHook, ProblemPluginMetadata) và PluginExtensionRegistry truy cập toàn cục.
- **Event Hook System**: Hệ thống hook vòng đời (OnStart, OnBeforeGenerate/AfterGenerate, OnBeforeRunSolution/AfterRunSolution, OnBeforeRunChecker/AfterRunChecker, OnAuditStart/Complete, OnBeforePackage/AfterPackage, OnShutdown) tích hợp vào pipeline sinh test và chạy lời giải.
- **ConfigStressTester CLI**: Tích hợp `--stress-test` lệnh dòng lệnh để stress test tham số tìm biên độ fail (TLE/MLE/WA), hỗ trợ format range (MIN,MAX[,STEP]) và giá trị tường minh (V1,V2,V3...), tích hợp reference solution phát hiện WA, báo cáo bảng thống kê và phân tích biên tự động, xuất JSON report.
- **CLI Search Command**: Thêm `--search <từ khóa>` để tìm kiếm lệnh/tùy chọn nhanh chóng qua 60+ lệnh, tìm theo tên lệnh, danh mục và mô tả; kết quả nhóm theo danh mục (Sinh test, Kiểm định, Đóng gói, Workspace, Plugin, Stress test, Thu nhỏ, Tiện ích); hỗ trợ shell completion (bash/PowerShell). Ví dụ: `ojgen --search zip`, `ojgen --search stress`, `ojgen --search solution`.
- **Ví dụ Plugin đầy đủ**: Thêm `examples/FullExamplePlugin/` demo tất cả extension points: custom problem, validator, checker, exporter, solution runner, event hook.
- **Tài liệu Plugin SDK**: Cập nhật `docs/PLUGIN_SDK.md` và `PLUGIN_GUIDE.md` tham chiếu đầy đủ với ví dụ code.

## 3.3.1 — 2026-11-14

- Sửa các lỗi hồi quy trong tạo test và thiết lập bài, gồm cấu hình đồ thị biên, nhận diện input lưới, tham số nhóm test và tiến độ CLI.
- Cải thiện độ tin cậy của kiểm định và đóng gói: xử lý manifest/checksum khi tạo lại output, dữ liệu nhóm trong provenance, thứ tự test tự nhiên và launcher validator.
- Sửa các vấn đề khi chạy lời giải và checker: áp dụng working directory từ run profile, làm mới cache biên dịch khi header thay đổi, truyền cờ cho C/C++/Pascal và cô lập tệp tạm.
- Gia cố soạn thảo đề, test tay, hàng đợi dự án và quét thư viện; xử lý tốt hơn dữ liệu không hợp lệ, thao tác hủy và trường hợp lỗi lưu/đọc.
- Bổ sung hồi quy cho các lỗi đã sửa và hoàn thiện pipeline CI trên GitLab.

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
- Thêm workspace `.ojproject` lưu cấu hình, statement, test tay/mẫu, kế hoạch nhóm và đường dẫn; khôi phục autosave sau khi khởi động lại.
- `--resume` và GUI tiếp tục cả bước tạo output; output đã khớp input, lời giải và SHA-256 được giữ lại.
- Thu nhỏ phản ví dụ theo cấu trúc cây, ma trận, lưới, truy vấn và nhiều bộ test; chỉ chạy lại ứng viên hợp lệ.
- Thêm mật độ đồ thị thưa/vừa/dày được tính theo số cạnh tối đa hợp lệ, kết hợp cờ liên thông/DAG/hai phía; giữ chế độ minM/maxM tự đặt.
- Khai báo ca chấp nhận/từ chối validator và checker trong `.ojcontract.json`; dùng chung cho GUI, audit và CLI.
- Đối chiếu các cận số trong statement với cấu hình nhóm; báo phạm vi n/m/q/t thực đo so với min/max của nhóm và nêu biên chưa chạm; ghi inventory CSV và provenance JSON có hash theo từng input/output.
- Mở rộng hồi quy CLI cho contract suite, checkpoint output và mật độ đồ thị; kiểm tra tĩnh GUI cho phím tắt dự án và preflight package.
- Kiểm tra cấu trúc, cặp input/output và checksum thực tế cho gói OjGen/Kattis; preflight release xác minh phiên bản, notes, ZIP và manifests.
- Thêm cửa sổ kiểm kê test có lọc, đổi tên/nhóm hàng loạt, min/max, kích thước, trùng lặp, thiếu output, CSV và trực quan đồ thị/cây/lưới/phân phối.
- Thêm ma trận nhiều lời giải để tìm test phân biệt với lời giải chuẩn; chạy lại validator input hoặc checker trên test cũ mà không sinh lại input/output.
- Kế hoạch nhóm hỗ trợ điểm, nhóm tiên quyết và thứ tự judge; package OjGen có thể lưu kế hoạch nhóm.
- Bổ sung checker scaffold Testlib, số thực, token không thứ tự và validator theo khuôn kèm ca hợp đồng tự sinh.
- Statement preview hỗ trợ bảng, ảnh HTTPS và các biểu thức TeX cơ bản, bên cạnh nội dung đa ngôn ngữ.
- Thêm run profile `.ojrun.json` cho lệnh build/run nhiều file; runner đơn file thêm C, Rust, Go, Pascal và Kotlin.
- Hàng đợi GUI tự lưu/khôi phục, đổi thứ tự, chỉnh seed/output/kế hoạch, giữ việc lỗi để thử lại.
- Benchmark báo median/p95/working set theo nhóm và ghi hệ điều hành, runtime, kiến trúc, số CPU logic.

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
