# StudyFlow AI

Website Hỗ trợ Học tập và Ôn thi Cá nhân hóa xây dựng trên **ASP.NET Core MVC (.NET 9)**, **Entity Framework Core (SQL Server)** và **Google Gemini AI**.

## Tính năng chính
1. **Quản lý Môn học, Tài liệu & Tóm tắt AI:** Tải lên PDF/DOCX/TXT, trích xuất nội dung tự động và tóm tắt bằng Gemini AI.
2. **Scan ảnh Thời khóa biểu (Gemini Vision):** Tải ảnh chụp TKB (`.jpg`, `.png`, `.webp`) để tự động trích xuất danh sách môn học (chống trùng) và lịch bận cố định trên trường.
3. **AI Quiz Hub:** Sinh đề trắc nghiệm tự động theo độ khó, thi thử đếm ngược, chấm điểm tức thì kèm giải thích & nhận xét AI.
4. **AI Study Planner & Pomodoro Focus:** Lập lịch ôn thi theo ngày thi, độ khó, điểm mục tiêu & lịch bận TKB trên FullCalendar (có hậu kiểm chống trùng giờ bận), tích hợp đồng hồ Pomodoro Focus.
5. **Dashboard Chart.js & Quản trị Admin:** Thống kê KPI, 3 biểu đồ Chart.js, trang tổng quan hệ thống và quản lý khóa/mở khóa tài khoản cho Admin.

---

## Hướng dẫn chạy dự án (dành cho thành viên nhóm)

### 1. Yêu cầu môi trường
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server LocalDB (đi kèm Visual Studio) hoặc SQL Server (chỉnh lại `ConnectionStrings:DefaultConnection` trong `StudyFlowAI/appsettings.json` nếu cần).

### 2. Clone & chạy lần đầu
```powershell
git clone https://github.com/Hauvotrung001elora/ELORA.git
cd ELORA/StudyFlowAI
dotnet restore
dotnet run
```
Khi chạy lần đầu, hệ thống (`DbSeeder`) sẽ **tự động tạo cơ sở dữ liệu** và khởi tạo tài khoản Admin mặc định:
- **Email:** `admin@studyflow.vn`
- **Mật khẩu:** `Admin@123`

### 3. Cấu hình Gemini API Key (tùy chọn để test tính năng AI)
> **Lưu ý bảo mật:** Tuyệt đối **không** ghi API Key trực tiếp vào `appsettings.json` rồi push lên GitHub. Hãy lưu bằng `.NET User Secrets`:
```powershell
cd StudyFlowAI
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY"
```

---

## Quy ước làm việc nhóm (Phần Hình ảnh & Giao diện UI)
Để chỉnh sửa hình ảnh, banner, CSS/giao diện mà **không ảnh hưởng đến chức năng backend**:
1. Luôn tạo nhánh mới trước khi sửa:
   ```powershell
   git checkout -b feat/ui-design
   ```
2. Các vùng sửa giao diện:
   - Hình ảnh / logo / banner: đặt trong `StudyFlowAI/wwwroot/images/`
   - CSS / hiệu ứng: `StudyFlowAI/wwwroot/css/site.css`, `StudyFlowAI/wwwroot/js/site.js`
   - Giao diện HTML/Bootstrap: `StudyFlowAI/Views/...`
3. Khi chỉnh sửa các file `.cshtml`, giữ nguyên các thuộc tính logic: `@model`, `asp-controller`, `asp-action`, `asp-route-*`, `name="..."`, `id="..."` (dùng bởi JS) và `@Html.AntiForgeryToken()`.
4. Sau khi hoàn tất, đẩy nhánh lên GitHub và tạo Pull Request vào nhánh `main`:
   ```powershell
   git add .
   git commit -m "Cap nhat giao dien va hinh anh"
   git push -u origin feat/ui-design
   ```
