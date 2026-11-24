using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyFlowAI.Data;
using StudyFlowAI.Models;
using StudyFlowAI.Services;
using StudyFlowAI.ViewModels;

namespace StudyFlowAI.Controllers;

[Authorize]
public class DeThiController : Controller
{
    private static readonly int[] AllowedQuestionCounts = [5, 10, 15, 20];
    private static readonly string[] AllowedDifficulties = ["De", "TrungBinh", "Kho"];

    private readonly ApplicationDbContext _context;
    private readonly GeminiService _geminiService;
    private readonly ILogger<DeThiController> _logger;

    public DeThiController(
        ApplicationDbContext context,
        GeminiService geminiService,
        ILogger<DeThiController> logger)
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

        var deThis = await _context.DeThis
            .AsNoTracking()
            .Include(d => d.TaiLieu)
                .ThenInclude(t => t!.MonHoc)
            .Include(d => d.CauHois)
            .Include(d => d.LuotLamBais.Where(l => l.MaNguoiDung == userId))
            .Where(d => d.TaiLieu != null && d.TaiLieu.MonHoc != null && d.TaiLieu.MonHoc.MaNguoiDung == userId)
            .OrderByDescending(d => d.NgayTao)
            .ToListAsync();

        var userAttempts = await _context.LuotLamBais
            .AsNoTracking()
            .Where(l => l.MaNguoiDung == userId)
            .Select(l => l.DiemSo)
            .ToListAsync();

        var vm = new DeThiIndexViewModel
        {
            DeThis = deThis,
            TongLuotLam = userAttempts.Count,
            DiemTrungBinh = userAttempts.Count > 0 ? Math.Round(userAttempts.Average(), 2) : null,
            IsGeminiConfigured = _geminiService.IsConfigured
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> TaoDe(int? maTaiLieu)
    {
        var userId = GetCurrentUserId();
        var options = await GetTaiLieuSelectListAsync(userId, maTaiLieu);

        if (options.Count == 0)
        {
            TempData["ErrorMessage"] = "Bạn cần tải lên ít nhất một tài liệu trước khi tạo đề trắc nghiệm AI.";
            return RedirectToAction("Upload", "TaiLieu");
        }

        var vm = new TaoDeThiViewModel
        {
            MaTaiLieu = maTaiLieu ?? int.Parse(options[0].Value),
            SoCau = 10,
            DoKho = "TrungBinh",
            ThoiGianLamPhut = 15,
            IsGeminiConfigured = _geminiService.IsConfigured,
            TaiLieuOptions = options
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoDe(TaoDeThiViewModel model)
    {
        var userId = GetCurrentUserId();
        model.TaiLieuOptions = await GetTaiLieuSelectListAsync(userId, model.MaTaiLieu);
        model.IsGeminiConfigured = _geminiService.IsConfigured;

        if (!AllowedQuestionCounts.Contains(model.SoCau))
        {
            ModelState.AddModelError(nameof(model.SoCau), "Vui lòng chọn số lượng câu hỏi là 5, 10, 15 hoặc 20.");
        }

        if (!AllowedDifficulties.Contains(model.DoKho))
        {
            ModelState.AddModelError(nameof(model.DoKho), "Độ khó không hợp lệ.");
        }

        var taiLieu = await _context.TaiLieus
            .AsNoTracking()
            .Include(t => t.MonHoc)
            .FirstOrDefaultAsync(t => t.MaTaiLieu == model.MaTaiLieu && t.MonHoc != null && t.MonHoc.MaNguoiDung == userId);

        if (taiLieu is null)
        {
            ModelState.AddModelError(nameof(model.MaTaiLieu), "Tài liệu không tồn tại hoặc không thuộc quyền sở hữu của bạn.");
        }
        else if (string.IsNullOrWhiteSpace(taiLieu.NoiDungTrichXuat))
        {
            ModelState.AddModelError(nameof(model.MaTaiLieu), "Tài liệu này không có nội dung văn bản để tạo đề.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var generatedQuestions = await _geminiService.TaoDeThiTracNghiemAsync(
                taiLieu!.NoiDungTrichXuat,
                model.SoCau,
                model.DoKho);

            var doKhoLabel = model.DoKho switch
            {
                "De" => "Dễ",
                "Kho" => "Khó",
                _ => "Trung bình"
            };

            var tieuDe = string.IsNullOrWhiteSpace(model.TieuDe)
                ? $"Đề ôn tập ({doKhoLabel} - {generatedQuestions.Count} câu): {taiLieu.TieuDe}"
                : model.TieuDe.Trim();

            if (tieuDe.Length > 255)
            {
                tieuDe = tieuDe[..255];
            }

            var deThi = new DeThi
            {
                MaTaiLieu = taiLieu.MaTaiLieu,
                TieuDe = tieuDe,
                DoKho = model.DoKho,
                ThoiGianLamPhut = model.ThoiGianLamPhut,
                NgayTao = DateTime.Now,
                CauHois = generatedQuestions.Select(q => new CauHoi
                {
                    NoiDung = q.NoiDung,
                    DapAnA = q.DapAnA,
                    DapAnB = q.DapAnB,
                    DapAnC = q.DapAnC,
                    DapAnD = q.DapAnD,
                    DapAnDung = q.DapAnDung,
                    GiaiThichAI = q.GiaiThichAI
                }).ToList()
            };

            _context.DeThis.Add(deThi);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Gemini AI đã tạo thành công đề thi gồm {deThi.CauHois.Count} câu hỏi. Chúc bạn làm bài tốt!";
            return RedirectToAction(nameof(LamBai), new { id = deThi.MaDeThi });
        }
        catch (GeminiApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong muốn khi tạo đề thi AI cho tài liệu {MaTaiLieu}", model.MaTaiLieu);
            ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi tạo đề thi. Vui lòng thử lại sau.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> LamBai(int id)
    {
        var userId = GetCurrentUserId();

        var deThi = await _context.DeThis
            .AsNoTracking()
            .Include(d => d.TaiLieu)
                .ThenInclude(t => t!.MonHoc)
            .Include(d => d.CauHois.OrderBy(c => c.MaCauHoi))
            .FirstOrDefaultAsync(d =>
                d.MaDeThi == id &&
                d.TaiLieu != null &&
                d.TaiLieu.MonHoc != null &&
                d.TaiLieu.MonHoc.MaNguoiDung == userId);

        if (deThi is null)
        {
            return NotFound();
        }

        if (deThi.CauHois.Count == 0)
        {
            TempData["ErrorMessage"] = "Đề thi này chưa có câu hỏi nào.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new LamBaiViewModel
        {
            DeThi = deThi,
            CauHois = deThi.CauHois.OrderBy(c => c.MaCauHoi).ToList(),
            ThoiGianBatDau = DateTime.Now
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NopBai(NopBaiInputModel input)
    {
        var userId = GetCurrentUserId();

        var deThi = await _context.DeThis
            .AsNoTracking()
            .Include(d => d.TaiLieu)
                .ThenInclude(t => t!.MonHoc)
            .Include(d => d.CauHois.OrderBy(c => c.MaCauHoi))
            .FirstOrDefaultAsync(d =>
                d.MaDeThi == input.MaDeThi &&
                d.TaiLieu != null &&
                d.TaiLieu.MonHoc != null &&
                d.TaiLieu.MonHoc.MaNguoiDung == userId);

        if (deThi is null)
        {
            return NotFound();
        }

        var cauHois = deThi.CauHois.OrderBy(c => c.MaCauHoi).ToList();
        var tongSoCau = cauHois.Count;
        if (tongSoCau == 0)
        {
            TempData["ErrorMessage"] = "Đề thi không có câu hỏi để chấm điểm.";
            return RedirectToAction(nameof(Index));
        }

        var traLois = input.TraLois ?? new Dictionary<int, string>();
        var chiTiets = new List<ChiTietTraLoi>(tongSoCau);
        var danhSachCauSai = new List<string>();
        var soCauDung = 0;

        foreach (var ch in cauHois)
        {
            traLois.TryGetValue(ch.MaCauHoi, out var selectedRaw);
            var dapAnChon = string.IsNullOrWhiteSpace(selectedRaw)
                ? null
                : selectedRaw.Trim().ToUpperInvariant();

            if (dapAnChon is not ("A" or "B" or "C" or "D"))
            {
                dapAnChon = null;
            }

            var dung = string.Equals(dapAnChon, ch.DapAnDung, StringComparison.OrdinalIgnoreCase);
            if (dung)
            {
                soCauDung++;
            }
            else
            {
                danhSachCauSai.Add($"{ch.NoiDung} (Đáp án đúng: {ch.DapAnDung})");
            }

            chiTiets.Add(new ChiTietTraLoi
            {
                MaCauHoi = ch.MaCauHoi,
                DapAnChon = dapAnChon,
                LaDapAnDung = dung
            });
        }

        var diemSo = Math.Round((double)soCauDung / tongSoCau * 10.0, 2);
        var thoiGianNop = DateTime.Now;
        var thoiGianBatDau = input.ThoiGianBatDau == default || input.ThoiGianBatDau > thoiGianNop
            ? thoiGianNop.AddMinutes(-1)
            : input.ThoiGianBatDau;

        string? nhanXetAI = null;
        if (_geminiService.IsConfigured)
        {
            try
            {
                nhanXetAI = await _geminiService.NhanXetKetQuaBaiThiAsync(deThi.TieuDe, diemSo, danhSachCauSai);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tạo nhận xét AI cho lượt làm bài đề {MaDeThi}", deThi.MaDeThi);
                nhanXetAI = "Chưa thể tải nhận xét từ Gemini AI ở thời điểm nộp bài. Bạn hãy xem chi tiết giải thích từng câu bên dưới để ôn lại các kiến thức cần củng cố.";
            }
        }
        else
        {
            nhanXetAI = "Chưa cấu hình Gemini API Key nên hệ thống tạm ẩn phần nhận xét tự động. Hãy xem phần giải thích chi tiết của từng câu bên dưới.";
        }

        var luotLamBai = new LuotLamBai
        {
            MaDeThi = deThi.MaDeThi,
            MaNguoiDung = userId,
            DiemSo = diemSo,
            SoCauDung = soCauDung,
            TongSoCau = tongSoCau,
            ThoiGianBatDau = thoiGianBatDau,
            ThoiGianNopBai = thoiGianNop,
            NhanXetAI = nhanXetAI,
            ChiTietTraLois = chiTiets
        };

        _context.LuotLamBais.Add(luotLamBai);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(KetQua), new { id = luotLamBai.MaLuotLam });
    }

    public async Task<IActionResult> KetQua(int id)
    {
        var userId = GetCurrentUserId();

        var luotLam = await _context.LuotLamBais
            .AsNoTracking()
            .Include(l => l.DeThi)
                .ThenInclude(d => d!.TaiLieu)
                    .ThenInclude(t => t!.MonHoc)
            .Include(l => l.ChiTietTraLois)
                .ThenInclude(ct => ct.CauHoi)
            .FirstOrDefaultAsync(l => l.MaLuotLam == id && l.MaNguoiDung == userId);

        if (luotLam is null)
        {
            return NotFound();
        }

        var vm = new KetQuaBaiThiViewModel
        {
            LuotLamBai = luotLam,
            ChiTietTraLois = luotLam.ChiTietTraLois.OrderBy(c => c.MaCauHoi).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> LichSu()
    {
        var userId = GetCurrentUserId();

        var attempts = await _context.LuotLamBais
            .AsNoTracking()
            .Include(l => l.DeThi)
                .ThenInclude(d => d!.TaiLieu)
                    .ThenInclude(t => t!.MonHoc)
            .Where(l => l.MaNguoiDung == userId)
            .OrderByDescending(l => l.ThoiGianNopBai ?? l.ThoiGianBatDau)
            .ToListAsync();

        var vm = new LichSuLamBaiViewModel
        {
            LuotLamBais = attempts,
            DiemTrungBinh = attempts.Count > 0 ? Math.Round(attempts.Average(a => a.DiemSo), 2) : null,
            DiemCaoNhat = attempts.Count > 0 ? attempts.Max(a => a.DiemSo) : null
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();

        var deThi = await _context.DeThis
            .Include(d => d.TaiLieu)
                .ThenInclude(t => t!.MonHoc)
            .FirstOrDefaultAsync(d =>
                d.MaDeThi == id &&
                d.TaiLieu != null &&
                d.TaiLieu.MonHoc != null &&
                d.TaiLieu.MonHoc.MaNguoiDung == userId);

        if (deThi is null)
        {
            return NotFound();
        }

        // Xóa chủ động bản ghi con để tránh lỗi khóa ngoại Restrict (ChiTietTraLoi -> CauHoi)
        var cauHois = await _context.CauHois.Where(c => c.MaDeThi == id).ToListAsync();
        var cauHoiIds = cauHois.Select(c => c.MaCauHoi).ToList();

        var luotLamBais = await _context.LuotLamBais.Where(l => l.MaDeThi == id).ToListAsync();
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

        var tieuDe = deThi.TieuDe;
        _context.DeThis.Remove(deThi);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã xóa đề thi \"{tieuDe}\".";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> GetTaiLieuSelectListAsync(string userId, int? selectedId)
    {
        var taiLieus = await _context.TaiLieus
            .AsNoTracking()
            .Include(t => t.MonHoc)
            .Where(t => t.MonHoc != null && t.MonHoc.MaNguoiDung == userId)
            .OrderByDescending(t => t.NgayTaiLen)
            .ToListAsync();

        return taiLieus.Select(t => new SelectListItem
        {
            Value = t.MaTaiLieu.ToString(),
            Text = $"[{t.MonHoc?.TenMonHoc}] {t.TieuDe}",
            Selected = selectedId == t.MaTaiLieu
        }).ToList();
    }
}
