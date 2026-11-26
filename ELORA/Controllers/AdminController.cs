using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlowAI.Data;
using StudyFlowAI.Models;
using StudyFlowAI.ViewModels;

namespace StudyFlowAI.Controllers;

[Authorize(Roles = DbSeeder.RoleAdmin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<NguoiDung> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        UserManager<NguoiDung> userManager,
        ILogger<AdminController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var nowUtc = DateTimeOffset.UtcNow;

        var tongNguoiDung = await _context.Users.AsNoTracking().CountAsync();
        var soTaiKhoanBiKhoa = await _context.Users
            .AsNoTracking()
            .CountAsync(u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > nowUtc);

        var tongMonHoc = await _context.MonHocs.AsNoTracking().CountAsync();
        var tongTaiLieu = await _context.TaiLieus.AsNoTracking().CountAsync();
        var tongDeThi = await _context.DeThis.AsNoTracking().CountAsync();
        var tongLuotLamBai = await _context.LuotLamBais.AsNoTracking().CountAsync();

        double? diemTbHeThong = null;
        if (tongLuotLamBai > 0)
        {
            diemTbHeThong = Math.Round(await _context.LuotLamBais.AsNoTracking().AverageAsync(l => l.DiemSo), 2);
        }

        var tongPhienPomodoro = await _context.PhienPomodoros.AsNoTracking().CountAsync();
        var tongPhutPomodoro = tongPhienPomodoro > 0
            ? await _context.PhienPomodoros.AsNoTracking().SumAsync(p => p.ThoiLuongPhut)
            : 0;

        // Top người dùng hoạt động tích cực nhất
        var topUsersRaw = await _context.Users
            .AsNoTracking()
            .Select(u => new AdminTopUserItem
            {
                Id = u.Id,
                HoTen = string.IsNullOrWhiteSpace(u.HoTen) ? (u.UserName ?? "Người dùng") : u.HoTen,
                Email = u.Email ?? u.UserName ?? string.Empty,
                SoMonHoc = u.MonHocs.Count,
                SoTaiLieu = u.MonHocs.SelectMany(m => m.TaiLieus).Count(),
                SoLuotLamBai = u.LuotLamBais.Count,
                SoPhienPomodoro = u.PhienPomodoros.Count
            })
            .ToListAsync();

        var topNguoiDung = topUsersRaw
            .OrderByDescending(u => u.TongHoatDong)
            .ThenByDescending(u => u.SoLuotLamBai)
            .Take(8)
            .ToList();

        // Tài liệu mới tải lên gần đây
        var taiLieuGanDay = await _context.TaiLieus
            .AsNoTracking()
            .OrderByDescending(t => t.NgayTaiLen)
            .Take(6)
            .Select(t => new AdminRecentDocumentItem
            {
                MaTaiLieu = t.MaTaiLieu,
                TieuDe = t.TieuDe,
                TenMonHoc = t.MonHoc != null ? t.MonHoc.TenMonHoc : "—",
                MaMau = t.MonHoc != null ? t.MonHoc.MaMau : "#4f46e5",
                NguoiTao = t.MonHoc != null && t.MonHoc.NguoiDung != null
                    ? (!string.IsNullOrWhiteSpace(t.MonHoc.NguoiDung.HoTen) ? t.MonHoc.NguoiDung.HoTen : (t.MonHoc.NguoiDung.Email ?? "—"))
                    : "—",
                DaTomTatAI = !string.IsNullOrWhiteSpace(t.TomTatAI),
                NgayTaiLen = t.NgayTaiLen
            })
            .ToListAsync();

        // Đề thi trắc nghiệm mới tạo gần đây
        var deThiGanDay = await _context.DeThis
            .AsNoTracking()
            .OrderByDescending(d => d.NgayTao)
            .Take(6)
            .Select(d => new AdminRecentQuizItem
            {
                MaDeThi = d.MaDeThi,
                TieuDe = d.TieuDe,
                TenMonHoc = d.TaiLieu != null && d.TaiLieu.MonHoc != null ? d.TaiLieu.MonHoc.TenMonHoc : "—",
                NguoiTao = d.TaiLieu != null && d.TaiLieu.MonHoc != null && d.TaiLieu.MonHoc.NguoiDung != null
                    ? (!string.IsNullOrWhiteSpace(d.TaiLieu.MonHoc.NguoiDung.HoTen)
                        ? d.TaiLieu.MonHoc.NguoiDung.HoTen
                        : (d.TaiLieu.MonHoc.NguoiDung.Email ?? "—"))
                    : "—",
                DoKho = d.DoKho,
                SoCauHoi = d.CauHois.Count,
                SoLuotLam = d.LuotLamBais.Count,
                NgayTao = d.NgayTao
            })
            .ToListAsync();

        var vm = new AdminDashboardViewModel
        {
            TongNguoiDung = tongNguoiDung,
            SoTaiKhoanBiKhoa = soTaiKhoanBiKhoa,
            TongMonHoc = tongMonHoc,
            TongTaiLieu = tongTaiLieu,
            TongDeThi = tongDeThi,
            TongLuotLamBai = tongLuotLamBai,
            DiemTrungBinhHeThong = diemTbHeThong,
            TongPhienPomodoro = tongPhienPomodoro,
            TongPhutPomodoro = tongPhutPomodoro,
            TopNguoiDung = topNguoiDung,
            TaiLieuGanDay = taiLieuGanDay,
            DeThiGanDay = deThiGanDay
        };

        return View(vm);
    }

    public async Task<IActionResult> Users(string? search, string? status)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var nowUtc = DateTimeOffset.UtcNow;

        var usersData = await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.NgayTao)
            .Select(u => new
            {
                u.Id,
                u.HoTen,
                u.Email,
                u.UserName,
                u.NgayTao,
                u.LockoutEnd,
                SoMonHoc = u.MonHocs.Count,
                SoTaiLieu = u.MonHocs.SelectMany(m => m.TaiLieus).Count()
            })
            .ToListAsync();

        // Lấy danh sách role cho từng user bằng 1 truy vấn join duy nhất
        var userRolesMap = await (
                from ur in _context.UserRoles.AsNoTracking()
                join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                select new { ur.UserId, RoleName = r.Name ?? string.Empty })
            .ToListAsync();

        var roleLookup = userRolesMap
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).Where(n => !string.IsNullOrEmpty(n)).ToList());

        var allRows = usersData.Select(u => new AdminUserRowViewModel
        {
            Id = u.Id,
            HoTen = string.IsNullOrWhiteSpace(u.HoTen) ? (u.UserName ?? "Người dùng") : u.HoTen,
            Email = u.Email ?? u.UserName ?? string.Empty,
            Roles = roleLookup.TryGetValue(u.Id, out var roles) ? roles : new List<string>(),
            NgayTao = u.NgayTao,
            SoMonHoc = u.SoMonHoc,
            SoTaiLieu = u.SoTaiLieu,
            IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > nowUtc,
            IsCurrentUser = u.Id == currentUserId
        }).ToList();

        var tongTaiKhoan = allRows.Count;
        var soBiKhoa = allRows.Count(x => x.IsLockedOut);
        var soHoatDong = tongTaiKhoan - soBiKhoa;

        IEnumerable<AdminUserRowViewModel> filtered = allRows;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            filtered = filtered.Where(u =>
                u.HoTen.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(u => !u.IsLockedOut);
        }
        else if (string.Equals(status, "locked", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(u => u.IsLockedOut);
        }

        var vm = new AdminUsersViewModel
        {
            Search = search?.Trim(),
            Status = status,
            TongTaiKhoan = tongTaiKhoan,
            SoDangHoatDong = soHoatDong,
            SoDangBiKhoa = soBiKhoa,
            Users = filtered.ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLockout(string userId, string? search, string? status)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["ErrorMessage"] = "Không xác định được tài khoản cần thao tác.";
            return RedirectToAction(nameof(Users), new { search, status });
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(userId, currentUserId, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "Bạn không thể tự khóa tài khoản Quản trị viên đang đăng nhập.";
            return RedirectToAction(nameof(Users), new { search, status });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tài khoản trong hệ thống.";
            return RedirectToAction(nameof(Users), new { search, status });
        }

        var isCurrentlyLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

        if (isCurrentlyLocked)
        {
            var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            if (unlockResult.Succeeded)
            {
                _logger.LogInformation("Admin đã mở khóa tài khoản {Email}.", user.Email);
                TempData["SuccessMessage"] = $"Đã mở khóa tài khoản \"{user.HoTen}\" ({user.Email}) thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể mở khóa tài khoản: " +
                    string.Join("; ", unlockResult.Errors.Select(e => e.Description));
            }
        }
        else
        {
            // Bắt buộc bật LockoutEnabled trước khi đặt LockoutEnd để PasswordSignInAsync chắc chắn chặn đăng nhập
            var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
            if (!enableResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Không thể kích hoạt trạng thái khóa cho tài khoản: " +
                    string.Join("; ", enableResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users), new { search, status });
            }

            var lockResult = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            if (lockResult.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                _logger.LogInformation("Admin đã khóa tài khoản {Email}.", user.Email);
                TempData["SuccessMessage"] = $"Đã khóa tài khoản \"{user.HoTen}\" ({user.Email}). Tài khoản này sẽ không thể đăng nhập.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể khóa tài khoản: " +
                    string.Join("; ", lockResult.Errors.Select(e => e.Description));
            }
        }

        return RedirectToAction(nameof(Users), new { search, status });
    }
}
