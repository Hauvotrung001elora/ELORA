using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlowAI.Data;
using StudyFlowAI.Models;
using StudyFlowAI.ViewModels;

namespace StudyFlowAI.Controllers;

[Authorize]
public class PomodoroController : Controller
{
    private readonly ApplicationDbContext _context;

    public PomodoroController(ApplicationDbContext context)
    {
        _context = context;
    }

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Không xác định được người dùng hiện tại.");

    public async Task<IActionResult> Index(int? maKeHoach)
    {
        var userId = GetCurrentUserId();

        var buoiHocChuaLam = await _context.KeHoachHocTaps
            .AsNoTracking()
            .Include(k => k.MonHoc)
            .Where(k => k.MonHoc != null && k.MonHoc.MaNguoiDung == userId && k.TrangThai == "ChuaLam")
            .OrderBy(k => k.NgayHoc)
            .ThenBy(k => k.GioBatDau)
            .ToListAsync();

        var selectedId = maKeHoach.HasValue && buoiHocChuaLam.Any(k => k.MaKeHoach == maKeHoach.Value)
            ? maKeHoach
            : null;

        var today = DateTime.Today;
        var diffFromMonday = ((int)today.DayOfWeek + 6) % 7;
        var startOfWeek = today.AddDays(-diffFromMonday);

        var weekSessions = await _context.PhienPomodoros
            .AsNoTracking()
            .Where(p => p.MaNguoiDung == userId && p.ThoiDiemHoanThanh >= startOfWeek)
            .Select(p => new { p.ThoiLuongPhut, p.ThoiDiemHoanThanh })
            .ToListAsync();

        var todaySessions = weekSessions.Where(p => p.ThoiDiemHoanThanh >= today).ToList();

        var recentHistory = await _context.PhienPomodoros
            .AsNoTracking()
            .Include(p => p.KeHoachHocTap)
                .ThenInclude(k => k!.MonHoc)
            .Where(p => p.MaNguoiDung == userId)
            .OrderByDescending(p => p.ThoiDiemHoanThanh)
            .Take(15)
            .ToListAsync();

        var vm = new PomodoroIndexViewModel
        {
            SelectedMaKeHoach = selectedId,
            BuoiHocChuaLam = buoiHocChuaLam,
            LichSuGanDay = recentHistory,
            SoPhienHomNay = todaySessions.Count,
            TongPhutHomNay = todaySessions.Sum(p => p.ThoiLuongPhut),
            SoPhienTuanNay = weekSessions.Count,
            TongPhutTuanNay = weekSessions.Sum(p => p.ThoiLuongPhut)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LuuPhien([FromBody] LuuPhienPomodoroRequest request)
    {
        if (request is null || request.ThoiLuongPhut < 1 || request.ThoiLuongPhut > 240)
        {
            return BadRequest(new { success = false, message = "Thời lượng phiên Pomodoro không hợp lệ (1 - 240 phút)." });
        }

        var userId = GetCurrentUserId();
        KeHoachHocTap? keHoach = null;

        if (request.MaKeHoach.HasValue)
        {
            keHoach = await _context.KeHoachHocTaps
                .Include(k => k.MonHoc)
                .FirstOrDefaultAsync(k =>
                    k.MaKeHoach == request.MaKeHoach.Value &&
                    k.MonHoc != null &&
                    k.MonHoc.MaNguoiDung == userId);
        }

        var phien = new PhienPomodoro
        {
            MaNguoiDung = userId,
            MaKeHoach = keHoach?.MaKeHoach,
            ThoiLuongPhut = request.ThoiLuongPhut,
            ThoiDiemHoanThanh = DateTime.Now
        };

        _context.PhienPomodoros.Add(phien);

        var markedCompleted = false;
        if (request.DanhDauHoanThanh && keHoach is not null && keHoach.TrangThai != "HoanThanh")
        {
            keHoach.TrangThai = "HoanThanh";
            markedCompleted = true;
        }

        await _context.SaveChangesAsync();

        var today = DateTime.Today;
        var diffFromMonday = ((int)today.DayOfWeek + 6) % 7;
        var startOfWeek = today.AddDays(-diffFromMonday);

        var weekSessions = await _context.PhienPomodoros
            .AsNoTracking()
            .Where(p => p.MaNguoiDung == userId && p.ThoiDiemHoanThanh >= startOfWeek)
            .Select(p => new { p.ThoiLuongPhut, p.ThoiDiemHoanThanh })
            .ToListAsync();

        var todaySessions = weekSessions.Where(p => p.ThoiDiemHoanThanh >= today).ToList();

        return Json(new
        {
            success = true,
            markedCompleted,
            soPhienHomNay = todaySessions.Count,
            tongPhutHomNay = todaySessions.Sum(p => p.ThoiLuongPhut),
            soPhienTuanNay = weekSessions.Count,
            tongPhutTuanNay = weekSessions.Sum(p => p.ThoiLuongPhut),
            session = new
            {
                maPhien = phien.MaPhien,
                thoiLuongPhut = phien.ThoiLuongPhut,
                thoiDiem = phien.ThoiDiemHoanThanh.ToString("dd/MM/yyyy HH:mm"),
                tieuDeBuoiHoc = keHoach?.TieuDe ?? "Học tự do",
                tenMonHoc = keHoach?.MonHoc?.TenMonHoc ?? "",
                maMau = keHoach?.MonHoc?.MaMau ?? "#64748b"
            }
        });
    }
}
