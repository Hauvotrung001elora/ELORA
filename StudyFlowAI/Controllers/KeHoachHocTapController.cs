using System.Globalization;
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
public class KeHoachHocTapController : Controller
{
    private static readonly string[] AllowedStatuses = ["ChuaLam", "HoanThanh", "BoLo"];

    private readonly ApplicationDbContext _context;
    private readonly GeminiService _geminiService;
    private readonly ILogger<KeHoachHocTapController> _logger;

    public KeHoachHocTapController(
        ApplicationDbContext context,
        GeminiService geminiService,
        ILogger<KeHoachHocTapController> logger)
    {
        _context = context;
        _geminiService = geminiService;
        _logger = logger;
    }

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Không xác định được người dùng hiện tại.");

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();

        var monHocs = await _context.MonHocs
            .AsNoTracking()
            .Where(m => m.MaNguoiDung == userId)
            .OrderBy(m => m.NgayThi == null)
            .ThenBy(m => m.NgayThi)
            .ThenBy(m => m.TenMonHoc)
            .ToListAsync();

        var taiLieus = await _context.TaiLieus
            .AsNoTracking()
            .Include(t => t.MonHoc)
            .Where(t => t.MonHoc != null && t.MonHoc.MaNguoiDung == userId)
            .OrderByDescending(t => t.NgayTaiLen)
            .ToListAsync();

        var statusCounts = await _context.KeHoachHocTaps
            .AsNoTracking()
            .Where(k => k.MonHoc != null && k.MonHoc.MaNguoiDung == userId)
            .GroupBy(k => k.TrangThai)
            .Select(g => new { TrangThai = g.Key, Count = g.Count() })
            .ToListAsync();

        var sinceDate = DateTime.Now.AddDays(-30);
        var weakQuizCount = await _context.LuotLamBais
            .AsNoTracking()
            .CountAsync(l => l.MaNguoiDung == userId && l.DiemSo < 6.5 && (l.ThoiGianNopBai ?? l.ThoiGianBatDau) >= sinceDate);

        var scoreStats = await GetSubjectScoreStatsAsync(userId);
        var prefilledSchedule = TempData[MonHocController.TempDataPlannerScheduleKey] as string;
        var autoOpenModal = string.Equals(
            TempData[MonHocController.TempDataOpenPlannerModalKey] as string,
            "true",
            StringComparison.OrdinalIgnoreCase);

        var vm = new KeHoachHocTapIndexViewModel
        {
            MonHocs = monHocs,
            TaiLieus = taiLieus,
            DiemTrungBinhTheoMon = scoreStats.ToDictionary(kv => kv.Key, kv => kv.Value.AverageScore),
            SoBuoiChuaLam = statusCounts.FirstOrDefault(x => x.TrangThai == "ChuaLam")?.Count ?? 0,
            SoBuoiHoanThanh = statusCounts.FirstOrDefault(x => x.TrangThai == "HoanThanh")?.Count ?? 0,
            SoBuoiBoLo = statusCounts.FirstOrDefault(x => x.TrangThai == "BoLo")?.Count ?? 0,
            SoBaiThiDiemThap = weakQuizCount,
            IsGeminiConfigured = _geminiService.IsConfigured,
            PrefilledKhungGioRanh = prefilledSchedule,
            AutoOpenGenerateModal = autoOpenModal
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> GetEvents(DateTime? start, DateTime? end)
    {
        var userId = GetCurrentUserId();

        var query = _context.KeHoachHocTaps
            .AsNoTracking()
            .Include(k => k.MonHoc)
            .Include(k => k.TaiLieu)
            .Where(k => k.MonHoc != null && k.MonHoc.MaNguoiDung == userId);

        if (start.HasValue)
        {
            query = query.Where(k => k.NgayHoc >= start.Value.Date.AddDays(-1));
        }

        if (end.HasValue)
        {
            query = query.Where(k => k.NgayHoc <= end.Value.Date.AddDays(1));
        }

        var items = await query
            .OrderBy(k => k.NgayHoc)
            .ThenBy(k => k.GioBatDau)
            .ToListAsync();

        var events = items.Select(k =>
        {
            var startDt = k.NgayHoc.Date.Add(k.GioBatDau);
            var endDt = startDt.AddMinutes(k.ThoiLuongPhut);

            var statusPrefix = k.TrangThai switch
            {
                "HoanThanh" => "✓ ",
                "BoLo" => "✕ ",
                _ => ""
            };
            var aiPrefix = k.DoAITao ? "[AI] " : "";
            var color = k.MonHoc?.MaMau ?? "#4f46e5";

            var classNames = new List<string>();
            if (k.TrangThai == "HoanThanh") classNames.Add("sf-fc-done");
            else if (k.TrangThai == "BoLo") classNames.Add("sf-fc-missed");

            return new
            {
                id = k.MaKeHoach.ToString(),
                title = $"{statusPrefix}{aiPrefix}{k.TieuDe}",
                start = startDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = endDt.ToString("yyyy-MM-ddTHH:mm:ss"),
                backgroundColor = k.TrangThai == "BoLo" ? "#94a3b8" : color,
                borderColor = k.TrangThai == "BoLo" ? "#64748b" : color,
                classNames,
                extendedProps = new
                {
                    maKeHoach = k.MaKeHoach,
                    maMonHoc = k.MaMonHoc,
                    tenMonHoc = k.MonHoc?.TenMonHoc ?? "",
                    maMau = color,
                    maTaiLieu = k.MaTaiLieu,
                    tenTaiLieu = k.TaiLieu?.TieuDe ?? "",
                    rawTieuDe = k.TieuDe,
                    ngayHoc = k.NgayHoc.ToString("yyyy-MM-dd"),
                    gioBatDau = k.GioBatDau.ToString(@"hh\:mm"),
                    thoiLuongPhut = k.ThoiLuongPhut,
                    trangThai = k.TrangThai,
                    doAITao = k.DoAITao
                }
            };
        });

        return Json(events);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KeHoachManualFormModel model)
    {
        var userId = GetCurrentUserId();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin buổi học chưa hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        var monHocOwned = await _context.MonHocs
            .AsNoTracking()
            .AnyAsync(m => m.MaMonHoc == model.MaMonHoc && m.MaNguoiDung == userId);

        if (!monHocOwned)
        {
            TempData["ErrorMessage"] = "Môn học không hợp lệ hoặc không thuộc quyền sở hữu của bạn.";
            return RedirectToAction(nameof(Index));
        }

        int? validMaTaiLieu = null;
        if (model.MaTaiLieu.HasValue)
        {
            var tlValid = await _context.TaiLieus
                .AsNoTracking()
                .AnyAsync(t => t.MaTaiLieu == model.MaTaiLieu.Value && t.MaMonHoc == model.MaMonHoc);
            if (tlValid)
            {
                validMaTaiLieu = model.MaTaiLieu.Value;
            }
        }

        var keHoach = new KeHoachHocTap
        {
            MaMonHoc = model.MaMonHoc,
            MaTaiLieu = validMaTaiLieu,
            TieuDe = model.TieuDe.Trim(),
            NgayHoc = model.NgayHoc.Date,
            GioBatDau = model.GioBatDau,
            ThoiLuongPhut = Math.Clamp(model.ThoiLuongPhut, 5, 480),
            TrangThai = "ChuaLam",
            DoAITao = false
        };

        _context.KeHoachHocTaps.Add(keHoach);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã thêm buổi học \"{keHoach.TieuDe}\" vào lịch.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(KeHoachManualFormModel model)
    {
        var userId = GetCurrentUserId();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin cập nhật buổi học chưa hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var keHoach = await _context.KeHoachHocTaps
            .Include(k => k.MonHoc)
            .FirstOrDefaultAsync(k => k.MaKeHoach == model.MaKeHoach && k.MonHoc != null && k.MonHoc.MaNguoiDung == userId);

        if (keHoach is null)
        {
            return NotFound();
        }

        var monHocOwned = await _context.MonHocs
            .AsNoTracking()
            .AnyAsync(m => m.MaMonHoc == model.MaMonHoc && m.MaNguoiDung == userId);

        if (!monHocOwned)
        {
            TempData["ErrorMessage"] = "Môn học không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        int? validMaTaiLieu = null;
        if (model.MaTaiLieu.HasValue)
        {
            var tlValid = await _context.TaiLieus
                .AsNoTracking()
                .AnyAsync(t => t.MaTaiLieu == model.MaTaiLieu.Value && t.MaMonHoc == model.MaMonHoc);
            if (tlValid)
            {
                validMaTaiLieu = model.MaTaiLieu.Value;
            }
        }

        keHoach.MaMonHoc = model.MaMonHoc;
        keHoach.MaTaiLieu = validMaTaiLieu;
        keHoach.TieuDe = model.TieuDe.Trim();
        keHoach.NgayHoc = model.NgayHoc.Date;
        keHoach.GioBatDau = model.GioBatDau;
        keHoach.ThoiLuongPhut = Math.Clamp(model.ThoiLuongPhut, 5, 480);
        if (AllowedStatuses.Contains(model.TrangThai))
        {
            keHoach.TrangThai = model.TrangThai;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã cập nhật buổi học \"{keHoach.TieuDe}\".";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string trangThai)
    {
        var userId = GetCurrentUserId();

        if (!AllowedStatuses.Contains(trangThai))
        {
            return BadRequest(new { success = false, message = "Trạng thái không hợp lệ." });
        }

        var keHoach = await _context.KeHoachHocTaps
            .Include(k => k.MonHoc)
            .FirstOrDefaultAsync(k => k.MaKeHoach == id && k.MonHoc != null && k.MonHoc.MaNguoiDung == userId);

        if (keHoach is null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy buổi học." });
        }

        keHoach.TrangThai = trangThai;
        await _context.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, trangThai });
        }

        TempData["SuccessMessage"] = "Đã cập nhật trạng thái buổi học.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();

        var keHoach = await _context.KeHoachHocTaps
            .Include(k => k.MonHoc)
            .FirstOrDefaultAsync(k => k.MaKeHoach == id && k.MonHoc != null && k.MonHoc.MaNguoiDung == userId);

        if (keHoach is null)
        {
            return NotFound();
        }

        // Gỡ liên kết DeleteBehavior.Restrict trên PhienPomodoro -> KeHoachHocTap trước khi xóa
        var relatedPomodoros = await _context.PhienPomodoros
            .Where(p => p.MaKeHoach == id)
            .ToListAsync();
        foreach (var p in relatedPomodoros)
        {
            p.MaKeHoach = null;
        }

        var tieuDe = keHoach.TieuDe;
        _context.KeHoachHocTaps.Remove(keHoach);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã xóa buổi học \"{tieuDe}\".";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoLichAI(TaoLichAIInputModel model)
    {
        var userId = GetCurrentUserId();

        if (model.SelectedMonHocIds is null || model.SelectedMonHocIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng tích chọn ít nhất một môn học để AI lập lịch.";
            return RedirectToAction(nameof(Index));
        }

        var ownedMonHocs = await _context.MonHocs
            .AsNoTracking()
            .Include(m => m.TaiLieus)
            .Where(m => m.MaNguoiDung == userId && model.SelectedMonHocIds.Contains(m.MaMonHoc))
            .ToListAsync();

        if (ownedMonHocs.Count == 0)
        {
            TempData["ErrorMessage"] = "Không tìm thấy môn học hợp lệ thuộc tài khoản của bạn.";
            return RedirectToAction(nameof(Index));
        }

        var scoreStats = await GetSubjectScoreStatsAsync(userId);

        var plannerInputs = ownedMonHocs.Select(m =>
        {
            var hasStat = scoreStats.TryGetValue(m.MaMonHoc, out var stat);
            return new PlannerSubjectInput
            {
                MaMonHoc = m.MaMonHoc,
                TenMonHoc = m.TenMonHoc,
                NgayThi = m.NgayThi,
                DiemMucTieu = m.DiemMucTieu,
                DiemTrungBinhHienTai = hasStat ? stat.AverageScore : null,
                SoLuotLamBai = hasStat ? stat.AttemptCount : 0,
                DoKho = m.DoKho,
                TaiLieus = m.TaiLieus.Select(t => new PlannerDocumentInput
                {
                    MaTaiLieu = t.MaTaiLieu,
                    TieuDe = t.TieuDe
                }).ToList()
            };
        }).ToList();

        var startDate = model.NgayBatDau.Date;
        var totalDays = Math.Clamp(model.SoNgay, 1, 30);
        var endDate = startDate.AddDays(totalDays - 1);

        // Chỉ giữ lại các buổi đã HoanThanh trong khoảng lập lịch để chống trùng giờ (các buổi chưa hoàn thành sẽ được thay thế sau khi AI tạo lịch thành công)
        var completedInRange = await _context.KeHoachHocTaps
            .AsNoTracking()
            .Where(k => k.MonHoc != null &&
                        k.MonHoc.MaNguoiDung == userId &&
                        k.TrangThai == "HoanThanh" &&
                        k.NgayHoc >= startDate &&
                        k.NgayHoc <= endDate)
            .ToListAsync();

        try
        {
            var generated = await _geminiService.TaoLichHocTapAsync(
                plannerInputs,
                startDate,
                totalDays,
                model.KhungGioRanh);

            var rawMappedSessions = ValidateAndMapSessions(generated, ownedMonHocs, startDate);

            // Lớp 2: Hậu kiểm & tự động dời các buổi trùng giờ BẬN / trùng buổi khác sang giờ RẢNH hợp lệ
            var constraints = ScheduleConstraintHelper.Parse(model.KhungGioRanh);
            var checkedResult = ScheduleConstraintHelper.FilterAndAdjustSessions(
                rawMappedSessions,
                constraints,
                completedInRange,
                startDate,
                endDate);

            if (checkedResult.AdjustedCount > 0 || checkedResult.DroppedCount > 0)
            {
                _logger.LogInformation(
                    "Hậu kiểm lịch AI (TaoLichAI) cho user {UserId}: chấp nhận {Accepted}, tự dời giờ {Adjusted}, loại bỏ do trùng giờ bận/kín lịch {Dropped}.",
                    userId,
                    checkedResult.AcceptedSessions.Count,
                    checkedResult.AdjustedCount,
                    checkedResult.DroppedCount);
            }

            if (checkedResult.AcceptedSessions.Count == 0)
            {
                TempData["ErrorMessage"] = "Không tìm được khung giờ rảnh hợp lệ nào không trùng lịch bận. Lịch học cũ của bạn vẫn được giữ nguyên — hãy mở rộng khung giờ rảnh và thử lại.";
                return RedirectToAction(nameof(Index));
            }

            // Chỉ sau khi có lịch mới hợp lệ mới tiến hành thay thế các buổi chưa hoàn thành cũ (giữ nguyên các buổi đã HoanThanh)
            var oldUnfinishedSessions = await _context.KeHoachHocTaps
                .Where(k => k.MonHoc != null &&
                            k.MonHoc.MaNguoiDung == userId &&
                            k.TrangThai != "HoanThanh")
                .ToListAsync();

            var replacedCount = oldUnfinishedSessions.Count;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            if (oldUnfinishedSessions.Count > 0)
            {
                var oldIds = oldUnfinishedSessions.Select(k => k.MaKeHoach).ToList();

                // Gỡ liên kết DeleteBehavior.Restrict trên PhienPomodoro -> KeHoachHocTap trước khi xóa lịch cũ
                var relatedPomodoros = await _context.PhienPomodoros
                    .Where(p => p.MaKeHoach.HasValue && oldIds.Contains(p.MaKeHoach.Value))
                    .ToListAsync();

                if (relatedPomodoros.Count > 0)
                {
                    foreach (var p in relatedPomodoros)
                    {
                        p.MaKeHoach = null;
                    }

                    await _context.SaveChangesAsync();
                }

                _context.KeHoachHocTaps.RemoveRange(oldUnfinishedSessions);
            }

            _context.KeHoachHocTaps.AddRange(checkedResult.AcceptedSessions);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var noteParts = new List<string>();
            if (replacedCount > 0)
            {
                noteParts.Add($"đã thay thế {replacedCount} buổi chưa hoàn thành trước đó");
            }
            if (checkedResult.AdjustedCount > 0)
            {
                noteParts.Add($"tự động điều chỉnh {checkedResult.AdjustedCount} buổi tránh giờ bận");
            }
            if (checkedResult.DroppedCount > 0)
            {
                noteParts.Add($"bỏ qua {checkedResult.DroppedCount} buổi trùng giờ bận/hết khung rảnh");
            }
            var extraNote = noteParts.Count > 0 ? $" ({string.Join(", ", noteParts)})" : string.Empty;

            TempData["SuccessMessage"] = $"Gemini AI đã tạo thành công {checkedResult.AcceptedSessions.Count} buổi học mới vào lịch của bạn{extraNote}.";
        }
        catch (GeminiApiException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong muốn khi lập lịch AI cho user {UserId}", userId);
            TempData["ErrorMessage"] = "Đã xảy ra lỗi khi lập lịch học bằng AI. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DieuChinhAI(DieuChinhLichAIInputModel model)
    {
        var userId = GetCurrentUserId();

        var ownedMonHocs = await _context.MonHocs
            .AsNoTracking()
            .Include(m => m.TaiLieus)
            .Where(m => m.MaNguoiDung == userId)
            .ToListAsync();

        if (ownedMonHocs.Count == 0)
        {
            TempData["ErrorMessage"] = "Bạn chưa có môn học nào để điều chỉnh lịch học.";
            return RedirectToAction(nameof(Index));
        }

        var missedSessions = await _context.KeHoachHocTaps
            .Include(k => k.MonHoc)
            .Where(k => k.MonHoc != null && k.MonHoc.MaNguoiDung == userId && k.TrangThai == "BoLo")
            .OrderByDescending(k => k.NgayHoc)
            .Take(20)
            .ToListAsync();

        var upcomingSessions = await _context.KeHoachHocTaps
            .AsNoTracking()
            .Include(k => k.MonHoc)
            .Where(k => k.MonHoc != null && k.MonHoc.MaNguoiDung == userId &&
                        k.TrangThai == "ChuaLam" && k.NgayHoc >= DateTime.Today)
            .OrderBy(k => k.NgayHoc)
            .ThenBy(k => k.GioBatDau)
            .Take(30)
            .ToListAsync();

        var sinceDate = DateTime.Now.AddDays(-30);
        var weakQuizzes = await _context.LuotLamBais
            .AsNoTracking()
            .Include(l => l.DeThi)
                .ThenInclude(d => d!.TaiLieu)
                    .ThenInclude(t => t!.MonHoc)
            .Where(l => l.MaNguoiDung == userId &&
                        l.DiemSo < 6.5 &&
                        (l.ThoiGianNopBai ?? l.ThoiGianBatDau) >= sinceDate &&
                        l.DeThi != null && l.DeThi.TaiLieu != null && l.DeThi.TaiLieu.MonHoc != null)
            .OrderByDescending(l => l.ThoiGianNopBai ?? l.ThoiGianBatDau)
            .Take(15)
            .ToListAsync();

        if (missedSessions.Count == 0 && weakQuizzes.Count == 0)
        {
            TempData["ErrorMessage"] = "Hiện bạn không có buổi học bị bỏ lỡ hay bài thi điểm thấp (< 6.5) nào cần điều chỉnh.";
            return RedirectToAction(nameof(Index));
        }

        var scoreStats = await GetSubjectScoreStatsAsync(userId);

        var subjectInputs = ownedMonHocs.Select(m =>
        {
            var hasStat = scoreStats.TryGetValue(m.MaMonHoc, out var stat);
            return new PlannerSubjectInput
            {
                MaMonHoc = m.MaMonHoc,
                TenMonHoc = m.TenMonHoc,
                NgayThi = m.NgayThi,
                DiemMucTieu = m.DiemMucTieu,
                DiemTrungBinhHienTai = hasStat ? stat.AverageScore : null,
                SoLuotLamBai = hasStat ? stat.AttemptCount : 0,
                DoKho = m.DoKho,
                TaiLieus = m.TaiLieus.Select(t => new PlannerDocumentInput
                {
                    MaTaiLieu = t.MaTaiLieu,
                    TieuDe = t.TieuDe
                }).ToList()
            };
        }).ToList();

        var missedInputs = missedSessions.Select(k => new PlannerSessionSummaryInput
        {
            MaMonHoc = k.MaMonHoc,
            TenMonHoc = k.MonHoc?.TenMonHoc ?? "",
            MaTaiLieu = k.MaTaiLieu,
            TieuDe = k.TieuDe,
            NgayHoc = k.NgayHoc,
            GioBatDau = k.GioBatDau,
            ThoiLuongPhut = k.ThoiLuongPhut
        }).ToList();

        var upcomingInputs = upcomingSessions.Select(k => new PlannerSessionSummaryInput
        {
            MaMonHoc = k.MaMonHoc,
            TenMonHoc = k.MonHoc?.TenMonHoc ?? "",
            MaTaiLieu = k.MaTaiLieu,
            TieuDe = k.TieuDe,
            NgayHoc = k.NgayHoc,
            GioBatDau = k.GioBatDau,
            ThoiLuongPhut = k.ThoiLuongPhut
        }).ToList();

        var weakQuizInputs = weakQuizzes.Select(l => new PlannerWeakQuizInput
        {
            MaMonHoc = l.DeThi!.TaiLieu!.MaMonHoc,
            TenMonHoc = l.DeThi.TaiLieu.MonHoc!.TenMonHoc,
            MaTaiLieu = l.DeThi.MaTaiLieu,
            TenTaiLieu = l.DeThi.TaiLieu.TieuDe,
            TenDeThi = l.DeThi.TieuDe,
            DiemSo = l.DiemSo,
            NgayLam = l.ThoiGianNopBai ?? l.ThoiGianBatDau
        }).ToList();

        var adjustStart = DateTime.Today;
        var adjustEnd = adjustStart.AddDays(13);

        try
        {
            var generated = await _geminiService.DieuChinhLichHocAsync(
                subjectInputs,
                missedInputs,
                upcomingInputs,
                weakQuizInputs,
                adjustStart,
                model.KhungGioRanh);

            var rawMappedSessions = ValidateAndMapSessions(generated, ownedMonHocs, adjustStart);

            // Lớp 2: Hậu kiểm chống trùng giờ BẬN và chống trùng các buổi CHƯA LÀM hiện có
            var constraints = ScheduleConstraintHelper.Parse(model.KhungGioRanh);
            var checkedResult = ScheduleConstraintHelper.FilterAndAdjustSessions(
                rawMappedSessions,
                constraints,
                upcomingSessions,
                adjustStart,
                adjustEnd);

            if (checkedResult.AdjustedCount > 0 || checkedResult.DroppedCount > 0)
            {
                _logger.LogInformation(
                    "Hậu kiểm lịch AI (DieuChinhAI) cho user {UserId}: chấp nhận {Accepted}, tự dời giờ {Adjusted}, loại bỏ {Dropped}.",
                    userId,
                    checkedResult.AcceptedSessions.Count,
                    checkedResult.AdjustedCount,
                    checkedResult.DroppedCount);
            }

            if (checkedResult.AcceptedSessions.Count == 0)
            {
                TempData["ErrorMessage"] = "Không tìm được khung giờ rảnh hợp lệ để xếp buổi ôn bù (tất cả đề xuất đều trùng giờ bận hoặc lịch đã có). Vui lòng mở rộng khung giờ rảnh và thử lại.";
                return RedirectToAction(nameof(Index));
            }

            // Nếu người dùng chọn xóa các buổi BoLo cũ (tuyệt đối không xóa buổi HoanThanh)
            if (model.XoaBuoiBoLoCu && missedSessions.Count > 0)
            {
                var missedIds = missedSessions.Select(m => m.MaKeHoach).ToList();
                var relatedPomodoros = await _context.PhienPomodoros
                    .Where(p => p.MaKeHoach.HasValue && missedIds.Contains(p.MaKeHoach.Value))
                    .ToListAsync();
                foreach (var p in relatedPomodoros)
                {
                    p.MaKeHoach = null;
                }

                _context.KeHoachHocTaps.RemoveRange(missedSessions);
            }

            _context.KeHoachHocTaps.AddRange(checkedResult.AcceptedSessions);
            await _context.SaveChangesAsync();

            var noteParts = new List<string>();
            if (checkedResult.AdjustedCount > 0)
            {
                noteParts.Add($"tự động điều chỉnh {checkedResult.AdjustedCount} buổi tránh giờ bận");
            }
            if (checkedResult.DroppedCount > 0)
            {
                noteParts.Add($"bỏ qua {checkedResult.DroppedCount} buổi trùng giờ bận/hết khung rảnh");
            }
            var extraNote = noteParts.Count > 0 ? $" ({string.Join(", ", noteParts)})" : string.Empty;

            TempData["SuccessMessage"] = $"Gemini AI đã điều chỉnh lịch và bổ sung {checkedResult.AcceptedSessions.Count} buổi ôn bù/củng cố mới{extraNote}.";
        }
        catch (GeminiApiException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong muốn khi điều chỉnh lịch AI cho user {UserId}", userId);
            TempData["ErrorMessage"] = "Đã xảy ra lỗi khi điều chỉnh lịch học bằng AI. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static List<KeHoachHocTap> ValidateAndMapSessions(
        List<GeneratedStudySessionDto> generated,
        List<MonHoc> ownedMonHocs,
        DateTime fallbackDate)
    {
        var validMonHocIds = ownedMonHocs.Select(m => m.MaMonHoc).ToHashSet();
        var validTaiLieuByMonHoc = ownedMonHocs.ToDictionary(
            m => m.MaMonHoc,
            m => m.TaiLieus.Select(t => t.MaTaiLieu).ToHashSet());

        var result = new List<KeHoachHocTap>();

        foreach (var item in generated)
        {
            // Kiểm tra quyền sở hữu MaMonHoc — nếu không khớp thì bỏ qua để tránh lỗi FK/IDOR
            if (!validMonHocIds.Contains(item.MaMonHoc))
            {
                continue;
            }

            // Kiểm tra MaTaiLieu phải thuộc đúng MaMonHoc — nếu không khớp thì gán null
            int? validTaiLieuId = null;
            if (item.MaTaiLieu.HasValue &&
                validTaiLieuByMonHoc.TryGetValue(item.MaMonHoc, out var tlSet) &&
                tlSet.Contains(item.MaTaiLieu.Value))
            {
                validTaiLieuId = item.MaTaiLieu.Value;
            }

            var ngayHoc = DateTime.TryParseExact(
                item.NgayHoc?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate)
                ? parsedDate.Date
                : fallbackDate.Date;

            var gioBatDau = TryParseTime(item.GioBatDau, out var parsedTime)
                ? parsedTime
                : new TimeSpan(19, 30, 0);

            var tieuDe = string.IsNullOrWhiteSpace(item.TieuDe)
                ? "Buổi ôn tập AI"
                : item.TieuDe.Trim();
            if (tieuDe.Length > 255)
            {
                tieuDe = tieuDe[..255];
            }

            result.Add(new KeHoachHocTap
            {
                MaMonHoc = item.MaMonHoc,
                MaTaiLieu = validTaiLieuId,
                TieuDe = tieuDe,
                NgayHoc = ngayHoc,
                GioBatDau = gioBatDau,
                ThoiLuongPhut = Math.Clamp(item.ThoiLuongPhut <= 0 ? 60 : item.ThoiLuongPhut, 15, 240),
                TrangThai = "ChuaLam",
                DoAITao = true
            });
        }

        return result;
    }

    private static bool TryParseTime(string? raw, out TimeSpan result)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var trimmed = raw.Trim();
            if (TimeSpan.TryParseExact(trimmed, @"hh\:mm", CultureInfo.InvariantCulture, out result) ||
                TimeSpan.TryParseExact(trimmed, @"h\:mm", CultureInfo.InvariantCulture, out result) ||
                TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out result))
            {
                if (result >= TimeSpan.Zero && result < TimeSpan.FromDays(1))
                {
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    private async Task<Dictionary<int, (double AverageScore, int AttemptCount)>> GetSubjectScoreStatsAsync(string userId)
    {
        var grouped = await _context.LuotLamBais
            .AsNoTracking()
            .Where(l => l.MaNguoiDung == userId && l.DeThi != null && l.DeThi.TaiLieu != null)
            .GroupBy(l => l.DeThi!.TaiLieu!.MaMonHoc)
            .Select(g => new
            {
                MaMonHoc = g.Key,
                AverageScore = g.Average(x => x.DiemSo),
                AttemptCount = g.Count()
            })
            .ToListAsync();

        return grouped.ToDictionary(
            x => x.MaMonHoc,
            x => (Math.Round(x.AverageScore, 2), x.AttemptCount));
    }
}
