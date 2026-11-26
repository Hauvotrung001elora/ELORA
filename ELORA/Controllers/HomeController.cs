using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlowAI.Data;
using StudyFlowAI.Models;
using StudyFlowAI.ViewModels;

namespace StudyFlowAI.Controllers;

public class HomeController : Controller
{
    private static readonly JsonSerializerOptions ChartJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<NguoiDung> _userManager;

    public HomeController(
        ApplicationDbContext context,
        UserManager<NguoiDung> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View(model: null);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return View(model: null);
        }

        var user = await _userManager.GetUserAsync(User);
        var hoTen = !string.IsNullOrWhiteSpace(user?.HoTen)
            ? user.HoTen
            : (User.Identity.Name ?? "Sinh viên");

        var today = DateTime.Today;
        var diffFromMonday = ((int)today.DayOfWeek + 6) % 7;
        var startOfWeek = today.AddDays(-diffFromMonday);
        var sevenDaysAgo = today.AddDays(-6);

        // 1. Môn học & Tài liệu của người dùng
        var monHocs = await _context.MonHocs
            .AsNoTracking()
            .Where(m => m.MaNguoiDung == userId)
            .Select(m => new
            {
                m.MaMonHoc,
                m.TenMonHoc,
                m.MaMau,
                m.NgayThi,
                m.DiemMucTieu,
                m.DoKho
            })
            .ToListAsync();

        var tongTaiLieu = await _context.TaiLieus
            .AsNoTracking()
            .CountAsync(t => t.MonHoc != null && t.MonHoc.MaNguoiDung == userId);

        // 2. Lượt làm bài trắc nghiệm của người dùng
        var luotLamBais = await _context.LuotLamBais
            .AsNoTracking()
            .Where(l => l.MaNguoiDung == userId)
            .Select(l => new
            {
                l.DiemSo,
                l.SoCauDung,
                l.TongSoCau,
                ThoiDiem = l.ThoiGianNopBai ?? l.ThoiGianBatDau,
                TenDeThi = l.DeThi != null ? l.DeThi.TieuDe : "Đề trắc nghiệm",
                TenMonHoc = l.DeThi != null && l.DeThi.TaiLieu != null && l.DeThi.TaiLieu.MonHoc != null
                    ? l.DeThi.TaiLieu.MonHoc.TenMonHoc
                    : string.Empty
            })
            .ToListAsync();

        var soLuotLamBai = luotLamBais.Count;
        double? diemTrungBinh = soLuotLamBai > 0
            ? Math.Round(luotLamBais.Average(l => l.DiemSo), 2)
            : null;

        // 3. Kế hoạch học tập của người dùng
        var keHoachs = await _context.KeHoachHocTaps
            .AsNoTracking()
            .Where(k => k.MonHoc != null && k.MonHoc.MaNguoiDung == userId)
            .Select(k => new
            {
                k.MaKeHoach,
                k.TieuDe,
                k.NgayHoc,
                k.GioBatDau,
                k.ThoiLuongPhut,
                k.TrangThai,
                k.DoAITao,
                TenMonHoc = k.MonHoc!.TenMonHoc,
                MaMau = k.MonHoc.MaMau
            })
            .ToListAsync();

        var keHoachHoanThanh = keHoachs.Count(k => k.TrangThai == "HoanThanh");
        var keHoachChuaLam = keHoachs.Count(k => k.TrangThai == "ChuaLam");
        var keHoachBoLo = keHoachs.Count(k => k.TrangThai == "BoLo");
        var tongKeHoach = keHoachs.Count;
        var tyLeHoanThanh = tongKeHoach > 0
            ? Math.Round(keHoachHoanThanh * 100.0 / tongKeHoach, 1)
            : 0;

        var buoiHocHomNay = keHoachs
            .Where(k => k.NgayHoc.Date == today && k.TrangThai == "ChuaLam")
            .OrderBy(k => k.GioBatDau)
            .Select(k => new KeHoachHomNayItem
            {
                MaKeHoach = k.MaKeHoach,
                TieuDe = k.TieuDe,
                TenMonHoc = k.TenMonHoc,
                MaMau = k.MaMau,
                GioBatDau = k.GioBatDau,
                ThoiLuongPhut = k.ThoiLuongPhut,
                DoAITao = k.DoAITao
            })
            .ToList();

        var monSapThi = monHocs
            .Where(m => m.NgayThi.HasValue && m.NgayThi.Value.Date >= today)
            .OrderBy(m => m.NgayThi!.Value)
            .Take(6)
            .Select(m => new MonSapThiItem
            {
                MaMonHoc = m.MaMonHoc,
                TenMonHoc = m.TenMonHoc,
                MaMau = m.MaMau,
                NgayThi = m.NgayThi!.Value,
                SoNgayConLai = (m.NgayThi.Value.Date - today).Days,
                DiemMucTieu = m.DiemMucTieu,
                DoKho = m.DoKho
            })
            .ToList();

        // 4. Phiên Pomodoro (lấy từ mốc sớm hơn giữa đầu tuần và 7 ngày gần nhất)
        var earliestPomoDate = startOfWeek < sevenDaysAgo ? startOfWeek : sevenDaysAgo;
        var pomoSessions = await _context.PhienPomodoros
            .AsNoTracking()
            .Where(p => p.MaNguoiDung == userId && p.ThoiDiemHoanThanh >= earliestPomoDate)
            .Select(p => new
            {
                p.ThoiLuongPhut,
                p.ThoiDiemHoanThanh
            })
            .ToListAsync();

        var weekPomos = pomoSessions.Where(p => p.ThoiDiemHoanThanh >= startOfWeek).ToList();
        var tongPhutTuanNay = weekPomos.Sum(p => p.ThoiLuongPhut);
        var soPhienTuanNay = weekPomos.Count;

        // 5. Chuẩn bị dữ liệu cho 3 biểu đồ Chart.js
        var recentScores = luotLamBais
            .OrderByDescending(l => l.ThoiDiem)
            .Take(10)
            .OrderBy(l => l.ThoiDiem)
            .ToList();

        var chartPayload = new DashboardChartPayload
        {
            PlanValues = [keHoachHoanThanh, keHoachChuaLam, keHoachBoLo]
        };

        foreach (var item in recentScores)
        {
            var shortTitle = item.TenDeThi.Length > 18
                ? item.TenDeThi[..18] + "…"
                : item.TenDeThi;

            chartPayload.ScoreLabels.Add($"{item.ThoiDiem:dd/MM} ({shortTitle})");
            chartPayload.ScoreValues.Add(Math.Round(item.DiemSo, 2));

            var subjectPart = string.IsNullOrWhiteSpace(item.TenMonHoc) ? string.Empty : $" - {item.TenMonHoc}";
            chartPayload.ScoreTooltips.Add($"{item.TenDeThi}{subjectPart} ({item.SoCauDung}/{item.TongSoCau} câu đúng)");
        }

        var pomoByDay = pomoSessions
            .Where(p => p.ThoiDiemHoanThanh.Date >= sevenDaysAgo)
            .GroupBy(p => p.ThoiDiemHoanThanh.Date)
            .ToDictionary(
                g => g.Key,
                g => new { Minutes = g.Sum(x => x.ThoiLuongPhut), Count = g.Count() });

        var tongPhut7Ngay = 0;
        for (var i = 0; i < 7; i++)
        {
            var day = sevenDaysAgo.AddDays(i);
            var label = day == today
                ? $"Hôm nay ({day:dd/MM})"
                : $"{GetShortVietnameseDayOfWeek(day.DayOfWeek)} {day:dd/MM}";

            var minutes = pomoByDay.TryGetValue(day, out var stat) ? stat.Minutes : 0;
            var count = stat?.Count ?? 0;
            tongPhut7Ngay += minutes;

            chartPayload.PomodoroLabels.Add(label);
            chartPayload.PomodoroMinutes.Add(minutes);
            chartPayload.PomodoroSessions.Add(count);
        }

        var vm = new DashboardViewModel
        {
            HoTen = hoTen,
            IsAdmin = User.IsInRole(DbSeeder.RoleAdmin),
            TongMonHoc = monHocs.Count,
            TongTaiLieu = tongTaiLieu,
            DiemTrungBinh = diemTrungBinh,
            SoLuotLamBai = soLuotLamBai,
            TongPhutPomodoroTuanNay = tongPhutTuanNay,
            SoPhienPomodoroTuanNay = soPhienTuanNay,
            TyLeHoanThanhKeHoach = tyLeHoanThanh,
            KeHoachHoanThanh = keHoachHoanThanh,
            KeHoachChuaLam = keHoachChuaLam,
            KeHoachBoLo = keHoachBoLo,
            TongKeHoach = tongKeHoach,
            BuoiHocHomNay = buoiHocHomNay,
            MonSapThi = monSapThi,
            HasScoreData = recentScores.Count > 0,
            HasPomodoroData = tongPhut7Ngay > 0,
            HasPlanData = tongKeHoach > 0,
            TongPhutPomodoro7Ngay = tongPhut7Ngay,
            ChartDataJson = JsonSerializer.Serialize(chartPayload, ChartJsonOptions)
        };

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static string GetShortVietnameseDayOfWeek(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        DayOfWeek.Sunday => "CN",
        _ => ""
    };
}
