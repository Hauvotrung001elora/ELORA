using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlowAI.Data;
using StudyFlowAI.Models;
using StudyFlowAI.Services;
using StudyFlowAI.ViewModels;

namespace StudyFlowAI.Controllers;

[Authorize]
public class MonHocController : Controller
{
    public const string TempDataPlannerScheduleKey = "PrefilledPlannerSchedule";
    public const string TempDataOpenPlannerModalKey = "OpenGenerateAiModal";

    private const long MaxTkbImageBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] PresetColors =
    [
        "#4f46e5", "#0ea5e9", "#10b981", "#f59e0b",
        "#ef4444", "#8b5cf6", "#ec4899", "#14b8a6"
    ];

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly GeminiService _geminiService;
    private readonly ILogger<MonHocController> _logger;

    public MonHocController(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        GeminiService geminiService,
        ILogger<MonHocController> logger)
    {
        _context = context;
        _env = env;
        _geminiService = geminiService;
        _logger = logger;
    }

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Không xác định được người dùng hiện tại.");

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();

        var subjects = await _context.MonHocs
            .AsNoTracking()
            .Where(m => m.MaNguoiDung == userId)
            .OrderBy(m => m.NgayThi == null)
            .ThenBy(m => m.NgayThi)
            .ThenByDescending(m => m.NgayTao)
            .Select(m => new MonHocCardViewModel
            {
                MaMonHoc = m.MaMonHoc,
                TenMonHoc = m.TenMonHoc,
                NgayThi = m.NgayThi,
                DiemMucTieu = m.DiemMucTieu,
                DoKho = m.DoKho,
                MaMau = m.MaMau,
                NgayTao = m.NgayTao,
                SoTaiLieu = m.TaiLieus.Count,
                SoDeThi = m.TaiLieus.SelectMany(t => t.DeThis).Count()
            })
            .ToListAsync();

        return View(subjects);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();

        var monHoc = await _context.MonHocs
            .AsNoTracking()
            .Include(m => m.TaiLieus.OrderByDescending(t => t.NgayTaiLen))
                .ThenInclude(t => t.DeThis)
            .FirstOrDefaultAsync(m => m.MaMonHoc == id && m.MaNguoiDung == userId);

        if (monHoc is null)
        {
            return NotFound();
        }

        var vm = new MonHocDetailsViewModel
        {
            MonHoc = monHoc,
            TaiLieus = monHoc.TaiLieus.OrderByDescending(t => t.NgayTaiLen).ToList(),
            TongSoDeThi = monHoc.TaiLieus.Sum(t => t.DeThis.Count)
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new MonHocFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MonHocFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = GetCurrentUserId();
        var monHoc = new MonHoc
        {
            MaNguoiDung = userId,
            TenMonHoc = model.TenMonHoc.Trim(),
            NgayThi = model.NgayThi,
            DiemMucTieu = model.DiemMucTieu,
            DoKho = model.DoKho,
            MaMau = string.IsNullOrWhiteSpace(model.MaMau) ? "#4f46e5" : model.MaMau.Trim(),
            NgayTao = DateTime.Now
        };

        _context.MonHocs.Add(monHoc);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã thêm môn học \"{monHoc.TenMonHoc}\" thành công.";
        return RedirectToAction(nameof(Details), new { id = monHoc.MaMonHoc });
    }

    [HttpGet]
    public IActionResult ScanTkb()
    {
        return View(new ScanTkbViewModel
        {
            IsGeminiConfigured = _geminiService.IsConfigured
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> ScanTkb(IFormFile? imageFile, CancellationToken cancellationToken)
    {
        var vm = new ScanTkbViewModel
        {
            IsGeminiConfigured = _geminiService.IsConfigured
        };

        if (imageFile is null || imageFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn một file ảnh Thời khóa biểu (.jpg, .png, .webp).";
            return View(vm);
        }

        if (imageFile.Length > MaxTkbImageBytes)
        {
            TempData["ErrorMessage"] = "Dung lượng ảnh vượt quá giới hạn tối đa 10 MB. Vui lòng chọn ảnh nhỏ hơn.";
            return View(vm);
        }

        var ext = Path.GetExtension(imageFile.FileName)?.ToLowerInvariant();
        var mimeType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        if (mimeType is null)
        {
            TempData["ErrorMessage"] = "Định dạng ảnh không được hỗ trợ. Chỉ chấp nhận các file .jpg, .jpeg, .png hoặc .webp.";
            return View(vm);
        }

        byte[] imageBytes;
        using (var ms = new MemoryStream())
        {
            await imageFile.CopyToAsync(ms, cancellationToken);
            imageBytes = ms.ToArray();
        }

        if (!IsValidImageSignature(imageBytes, mimeType))
        {
            TempData["ErrorMessage"] = "File tải lên không phải ảnh JPG/PNG/WEBP hợp lệ. Vui lòng kiểm tra lại file ảnh.";
            return View(vm);
        }

        try
        {
            var scanResult = await _geminiService.ScanThoiKhoaBieuAsync(imageBytes, mimeType, cancellationToken);

            var userId = GetCurrentUserId();
            var existingSubjectNames = await _context.MonHocs
                .AsNoTracking()
                .Where(m => m.MaNguoiDung == userId)
                .Select(m => m.TenMonHoc)
                .ToListAsync(cancellationToken);

            var existingSet = new HashSet<string>(
                existingSubjectNames.Select(NormalizeSubjectKey),
                StringComparer.OrdinalIgnoreCase);

            var previewSubjects = new List<ScanTkbSubjectItemViewModel>();
            for (var i = 0; i < scanResult.DanhSachMonHoc.Count; i++)
            {
                var name = scanResult.DanhSachMonHoc[i];
                var alreadyExists = existingSet.Contains(NormalizeSubjectKey(name));

                previewSubjects.Add(new ScanTkbSubjectItemViewModel
                {
                    Chon = !alreadyExists,
                    TenMonHoc = name,
                    DoKho = 2,
                    MaMau = PresetColors[i % PresetColors.Length],
                    DaTonTai = alreadyExists
                });
            }

            vm.HasResult = true;
            vm.UploadedFileName = Path.GetFileName(imageFile.FileName);
            vm.LichBanTuTkb = scanResult.LichBanTrenTruong;
            vm.Subjects = previewSubjects;

            // Hiển thị ảnh đối chiếu nếu ảnh <= 4MB để giao diện nhẹ và mượt
            if (imageBytes.Length <= 4 * 1024 * 1024)
            {
                vm.ImagePreviewDataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";
            }

            if (previewSubjects.Count == 0 && string.IsNullOrWhiteSpace(scanResult.LichBanTrenTruong))
            {
                TempData["ErrorMessage"] = "AI không nhận diện được môn học hay khung giờ nào trong ảnh. Bạn hãy thử ảnh chụp rõ nét hơn hoặc nhập thủ công bên dưới.";
            }

            return View(vm);
        }
        catch (GeminiApiException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong muốn khi quét ảnh thời khóa biểu.");
            TempData["ErrorMessage"] = "Đã xảy ra lỗi trong quá trình phân tích ảnh thời khóa biểu. Vui lòng thử lại.";
            return View(vm);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LuuTuTkb(ConfirmScanTkbInputModel model, string? submitAction)
    {
        var userId = GetCurrentUserId();
        var goToPlanner = model.DiToiLapLichAI ||
            string.Equals(submitAction, "planner", StringComparison.OrdinalIgnoreCase);

        var existingSubjectNames = await _context.MonHocs
            .AsNoTracking()
            .Where(m => m.MaNguoiDung == userId)
            .Select(m => m.TenMonHoc)
            .ToListAsync();

        var existingSet = new HashSet<string>(
            existingSubjectNames.Select(NormalizeSubjectKey),
            StringComparer.OrdinalIgnoreCase);

        var newSubjects = new List<MonHoc>();
        var skippedDuplicateCount = 0;

        foreach (var item in model.Subjects ?? new List<ScanTkbSubjectItemViewModel>())
        {
            if (!item.Chon)
            {
                continue;
            }

            var cleanName = CleanSubjectName(item.TenMonHoc);
            if (string.IsNullOrWhiteSpace(cleanName))
            {
                continue;
            }

            var key = NormalizeSubjectKey(cleanName);
            if (!existingSet.Add(key))
            {
                skippedDuplicateCount++;
                continue;
            }

            double? diemMucTieu = item.DiemMucTieu.HasValue
                ? Math.Round(Math.Clamp(item.DiemMucTieu.Value, 0, 10), 2)
                : null;

            var color = !string.IsNullOrWhiteSpace(item.MaMau) && item.MaMau.Trim().Length <= 20
                ? item.MaMau.Trim()
                : PresetColors[newSubjects.Count % PresetColors.Length];

            newSubjects.Add(new MonHoc
            {
                MaNguoiDung = userId,
                TenMonHoc = cleanName.Length > 200 ? cleanName[..200] : cleanName,
                NgayThi = item.NgayThi?.Date,
                DiemMucTieu = diemMucTieu,
                DoKho = Math.Clamp(item.DoKho, 1, 3),
                MaMau = color,
                NgayTao = DateTime.Now
            });
        }

        if (newSubjects.Count > 0)
        {
            _context.MonHocs.AddRange(newSubjects);
            await _context.SaveChangesAsync();
        }

        var combinedSchedule = BuildCombinedScheduleText(model.LichBanTuTkb, model.LichCaNhanBoSung);
        if (!string.IsNullOrWhiteSpace(combinedSchedule))
        {
            TempData[TempDataPlannerScheduleKey] = combinedSchedule;
        }

        if (newSubjects.Count > 0)
        {
            var dupNote = skippedDuplicateCount > 0
                ? $" (đã bỏ qua {skippedDuplicateCount} môn trùng tên)"
                : string.Empty;
            TempData["SuccessMessage"] = $"Đã lưu thành công {newSubjects.Count} môn học mới từ Thời khóa biểu{dupNote}.";
        }
        else if (goToPlanner && !string.IsNullOrWhiteSpace(combinedSchedule))
        {
            var dupNote = skippedDuplicateCount > 0
                ? $" ({skippedDuplicateCount} môn đã có sẵn nên không tạo trùng)"
                : string.Empty;
            TempData["SuccessMessage"] = $"Đã chuyển thông tin lịch bận từ TKB sang AI Study Planner{dupNote}.";
        }
        else if (skippedDuplicateCount > 0)
        {
            TempData["ErrorMessage"] = $"Tất cả {skippedDuplicateCount} môn học được chọn đều đã tồn tại trong danh sách của bạn nên không tạo thêm môn trùng.";
            return RedirectToAction(nameof(Index));
        }
        else
        {
            TempData["ErrorMessage"] = "Chưa có môn học nào được chọn để lưu.";
            return RedirectToAction(nameof(ScanTkb));
        }

        if (goToPlanner)
        {
            var hasAnySubject = newSubjects.Count > 0 || existingSubjectNames.Count > 0;
            if (hasAnySubject)
            {
                TempData[TempDataOpenPlannerModalKey] = "true";
                return RedirectToAction("Index", "KeHoachHocTap");
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetCurrentUserId();
        var monHoc = await _context.MonHocs
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MaMonHoc == id && m.MaNguoiDung == userId);

        if (monHoc is null)
        {
            return NotFound();
        }

        var vm = new MonHocFormViewModel
        {
            MaMonHoc = monHoc.MaMonHoc,
            TenMonHoc = monHoc.TenMonHoc,
            NgayThi = monHoc.NgayThi,
            DiemMucTieu = monHoc.DiemMucTieu,
            DoKho = monHoc.DoKho,
            MaMau = monHoc.MaMau
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MonHocFormViewModel model)
    {
        if (id != model.MaMonHoc)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = GetCurrentUserId();
        var monHoc = await _context.MonHocs
            .FirstOrDefaultAsync(m => m.MaMonHoc == id && m.MaNguoiDung == userId);

        if (monHoc is null)
        {
            return NotFound();
        }

        monHoc.TenMonHoc = model.TenMonHoc.Trim();
        monHoc.NgayThi = model.NgayThi;
        monHoc.DiemMucTieu = model.DiemMucTieu;
        monHoc.DoKho = model.DoKho;
        monHoc.MaMau = string.IsNullOrWhiteSpace(model.MaMau) ? "#4f46e5" : model.MaMau.Trim();

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã cập nhật môn học \"{monHoc.TenMonHoc}\".";
        return RedirectToAction(nameof(Details), new { id = monHoc.MaMonHoc });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        var monHoc = await _context.MonHocs
            .FirstOrDefaultAsync(m => m.MaMonHoc == id && m.MaNguoiDung == userId);

        if (monHoc is null)
        {
            return NotFound();
        }

        // Thu thập danh sách file vật lý cần xóa sau khi xóa DB thành công
        var taiLieus = await _context.TaiLieus
            .Where(t => t.MaMonHoc == id)
            .ToListAsync();
        var taiLieuIds = taiLieus.Select(t => t.MaTaiLieu).ToList();
        var filePathsToDelete = taiLieus
            .Select(t => t.DuongDanFile)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        // 1. Gỡ liên kết Restrict trên PhienPomodoro -> KeHoachHocTap và KeHoachHocTap -> TaiLieu
        var keHoachs = await _context.KeHoachHocTaps
            .Where(k => k.MaMonHoc == id || (k.MaTaiLieu.HasValue && taiLieuIds.Contains(k.MaTaiLieu.Value)))
            .ToListAsync();

        if (keHoachs.Count > 0)
        {
            var keHoachIds = keHoachs.Select(k => k.MaKeHoach).ToList();
            var pomodoros = await _context.PhienPomodoros
                .Where(p => p.MaKeHoach.HasValue && keHoachIds.Contains(p.MaKeHoach.Value))
                .ToListAsync();
            foreach (var p in pomodoros)
            {
                p.MaKeHoach = null;
            }

            foreach (var k in keHoachs)
            {
                k.MaTaiLieu = null;
            }

            await _context.SaveChangesAsync();
            _context.KeHoachHocTaps.RemoveRange(keHoachs.Where(k => k.MaMonHoc == id));
        }

        // 2. Xóa các bản ghi con của DeThi (tránh Restrict ChiTietTraLoi -> CauHoi)
        if (taiLieuIds.Count > 0)
        {
            var deThis = await _context.DeThis
                .Where(d => taiLieuIds.Contains(d.MaTaiLieu))
                .ToListAsync();
            var deThiIds = deThis.Select(d => d.MaDeThi).ToList();

            if (deThiIds.Count > 0)
            {
                var cauHois = await _context.CauHois
                    .Where(c => deThiIds.Contains(c.MaDeThi))
                    .ToListAsync();
                var cauHoiIds = cauHois.Select(c => c.MaCauHoi).ToList();

                var luotLamBais = await _context.LuotLamBais
                    .Where(l => deThiIds.Contains(l.MaDeThi))
                    .ToListAsync();
                var luotLamIds = luotLamBais.Select(l => l.MaLuotLam).ToList();

                var chiTietTraLois = await _context.ChiTietTraLois
                    .Where(ct => luotLamIds.Contains(ct.MaLuotLam) || cauHoiIds.Contains(ct.MaCauHoi))
                    .ToListAsync();

                if (chiTietTraLois.Count > 0)
                {
                    _context.ChiTietTraLois.RemoveRange(chiTietTraLois);
                }

                if (luotLamBais.Count > 0)
                {
                    _context.LuotLamBais.RemoveRange(luotLamBais);
                }

                if (cauHois.Count > 0)
                {
                    _context.CauHois.RemoveRange(cauHois);
                }

                _context.DeThis.RemoveRange(deThis);
            }

            _context.TaiLieus.RemoveRange(taiLieus);
        }

        var tenMon = monHoc.TenMonHoc;
        _context.MonHocs.Remove(monHoc);
        await _context.SaveChangesAsync();

        // 3. Xóa file vật lý trong wwwroot/uploads
        foreach (var relativePath in filePathsToDelete)
        {
            DeletePhysicalFileSafely(relativePath);
        }

        TempData["SuccessMessage"] = $"Đã xóa môn học \"{tenMon}\" và toàn bộ tài liệu, đề thi liên quan.";
        return RedirectToAction(nameof(Index));
    }

    private void DeletePhysicalFileSafely(string relativePath)
    {
        try
        {
            var trimmed = relativePath.TrimStart('/', '\\');
            if (!trimmed.StartsWith("uploads", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fullPath = Path.Combine(_env.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể xóa file vật lý {Path}", relativePath);
        }
    }

    private static string CleanSubjectName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        return string.Join(' ', rawName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeSubjectKey(string? rawName) =>
        CleanSubjectName(rawName).ToLowerInvariant();

    private static bool IsValidImageSignature(byte[] bytes, string mimeType)
    {
        if (bytes.Length < 12)
        {
            return false;
        }

        return mimeType switch
        {
            "image/jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
            "image/webp" => bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P',
            _ => false
        };
    }

    private static string BuildCombinedScheduleText(string? lichBanTuTkb, string? lichCaNhanBoSung)
    {
        var tkb = lichBanTuTkb?.Trim();
        var personal = lichCaNhanBoSung?.Trim();

        string combined;
        if (!string.IsNullOrWhiteSpace(tkb) && !string.IsNullOrWhiteSpace(personal))
        {
            combined = $"Lịch bận trên trường (từ TKB): {tkb}\nLịch cá nhân / khung giờ rảnh bổ sung: {personal}";
        }
        else if (!string.IsNullOrWhiteSpace(tkb))
        {
            combined = $"Lịch bận trên trường (từ TKB): {tkb}\nKhung giờ rảnh tự học: Các khung giờ trống ngoài lịch trên (ưu tiên buổi tối 19:30-21:30 và cuối tuần).";
        }
        else if (!string.IsNullOrWhiteSpace(personal))
        {
            combined = personal;
        }
        else
        {
            return string.Empty;
        }

        return combined.Length > 2000 ? combined[..2000] : combined;
    }
}
