using System.Security.Claims;
using System.Text.RegularExpressions;
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
public class TaiLieuController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly DocumentExtractorService _extractor;
    private readonly GeminiService _geminiService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TaiLieuController> _logger;

    public TaiLieuController(
        ApplicationDbContext context,
        DocumentExtractorService extractor,
        GeminiService geminiService,
        IWebHostEnvironment env,
        ILogger<TaiLieuController> logger)
    {
        _context = context;
        _extractor = extractor;
        _geminiService = geminiService;
        _env = env;
        _logger = logger;
    }

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Không xác định được người dùng hiện tại.");

    public async Task<IActionResult> Index(int? maMonHoc)
    {
        var userId = GetCurrentUserId();

        var monHocs = await _context.MonHocs
            .AsNoTracking()
            .Where(m => m.MaNguoiDung == userId)
            .OrderBy(m => m.TenMonHoc)
            .ToListAsync();

        var query = _context.TaiLieus
            .AsNoTracking()
            .Include(t => t.MonHoc)
            .Include(t => t.DeThis)
            .Where(t => t.MonHoc != null && t.MonHoc.MaNguoiDung == userId);

        if (maMonHoc.HasValue)
        {
            query = query.Where(t => t.MaMonHoc == maMonHoc.Value);
        }

        var vm = new TaiLieuIndexViewModel
        {
            FilterMaMonHoc = maMonHoc,
            MonHocOptions = monHocs.Select(m => new SelectListItem
            {
                Value = m.MaMonHoc.ToString(),
                Text = m.TenMonHoc,
                Selected = maMonHoc == m.MaMonHoc
            }).ToList(),
            TaiLieus = await query.OrderByDescending(t => t.NgayTaiLen).ToListAsync()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Upload(int? maMonHoc)
    {
        var userId = GetCurrentUserId();
        var options = await GetMonHocSelectListAsync(userId, maMonHoc);

        if (options.Count == 0)
        {
            TempData["ErrorMessage"] = "Bạn cần tạo ít nhất một môn học trước khi tải tài liệu lên.";
            return RedirectToAction("Create", "MonHoc");
        }

        var vm = new TaiLieuUploadViewModel
        {
            MaMonHoc = maMonHoc ?? int.Parse(options[0].Value),
            MonHocOptions = options
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(DocumentExtractorService.MaxFileSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(TaiLieuUploadViewModel model)
    {
        var userId = GetCurrentUserId();
        model.MonHocOptions = await GetMonHocSelectListAsync(userId, model.MaMonHoc);

        var monHoc = await _context.MonHocs
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MaMonHoc == model.MaMonHoc && m.MaNguoiDung == userId);

        if (monHoc is null)
        {
            ModelState.AddModelError(nameof(model.MaMonHoc), "Môn học không hợp lệ hoặc không thuộc quyền sở hữu của bạn.");
        }

        if (model.TepTaiLieu is null || model.TepTaiLieu.Length == 0)
        {
            ModelState.AddModelError(nameof(model.TepTaiLieu), "Vui lòng chọn tệp tài liệu có nội dung.");
        }
        else
        {
            if (!_extractor.IsSupportedExtension(model.TepTaiLieu.FileName))
            {
                ModelState.AddModelError(nameof(model.TepTaiLieu), "Chỉ hỗ trợ các định dạng .pdf, .docx và .txt.");
            }

            if (model.TepTaiLieu.Length > DocumentExtractorService.MaxFileSizeBytes)
            {
                ModelState.AddModelError(nameof(model.TepTaiLieu), "Dung lượng tệp vượt quá giới hạn tối đa 20 MB.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var file = model.TepTaiLieu!;
        var originalFileName = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var safeBaseName = Regex.Replace(baseName, @"[^a-zA-Z0-9_\-\.]", "_");
        if (safeBaseName.Length > 60)
        {
            safeBaseName = safeBaseName[..60];
        }

        var storedFileName = $"{Guid.NewGuid():N}_{safeBaseName}{ext}";
        var userUploadDir = Path.Combine(_env.WebRootPath, "uploads", userId);
        Directory.CreateDirectory(userUploadDir);

        var physicalPath = Path.Combine(userUploadDir, storedFileName);
        await using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var extraction = await _extractor.ExtractTextAsync(physicalPath);
        if (!extraction.Success)
        {
            try
            {
                System.IO.File.Delete(physicalPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể dọn file tạm {Path}", physicalPath);
            }

            ModelState.AddModelError(nameof(model.TepTaiLieu), extraction.ErrorMessage ?? "Không thể trích xuất văn bản từ tệp.");
            return View(model);
        }

        var tieuDe = string.IsNullOrWhiteSpace(model.TieuDe)
            ? baseName
            : model.TieuDe.Trim();

        if (tieuDe.Length > 255)
        {
            tieuDe = tieuDe[..255];
        }

        var taiLieu = new TaiLieu
        {
            MaMonHoc = model.MaMonHoc,
            TieuDe = tieuDe,
            DuongDanFile = $"/uploads/{userId}/{storedFileName}",
            NoiDungTrichXuat = extraction.Text,
            NgayTaiLen = DateTime.Now
        };

        _context.TaiLieus.Add(taiLieu);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tải lên và trích xuất thành công tài liệu \"{taiLieu.TieuDe}\".";
        return RedirectToAction(nameof(Details), new { id = taiLieu.MaTaiLieu });
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();

        var taiLieu = await _context.TaiLieus
            .AsNoTracking()
            .Include(t => t.MonHoc)
            .Include(t => t.DeThis.OrderByDescending(d => d.NgayTao))
                .ThenInclude(d => d.CauHois)
            .Include(t => t.DeThis)
                .ThenInclude(d => d.LuotLamBais)
            .FirstOrDefaultAsync(t => t.MaTaiLieu == id && t.MonHoc != null && t.MonHoc.MaNguoiDung == userId);

        if (taiLieu is null)
        {
            return NotFound();
        }

        var fileName = Path.GetFileName(taiLieu.DuongDanFile);
        var underscoreIdx = fileName.IndexOf('_');
        var displayFileName = underscoreIdx >= 0 && underscoreIdx < fileName.Length - 1
            ? fileName[(underscoreIdx + 1)..]
            : fileName;

        var text = taiLieu.NoiDungTrichXuat ?? string.Empty;
        var wordCount = string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        var vm = new TaiLieuDetailsViewModel
        {
            TaiLieu = taiLieu,
            TenFileGoc = displayFileName,
            DinhDangFile = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant(),
            SoKyTuTrichXuat = text.Length,
            SoTuUocTinh = wordCount,
            IsGeminiConfigured = _geminiService.IsConfigured,
            DeThis = taiLieu.DeThis.OrderByDescending(d => d.NgayTao).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoTomTat(int id)
    {
        var userId = GetCurrentUserId();
        var taiLieu = await _context.TaiLieus
            .Include(t => t.MonHoc)
            .FirstOrDefaultAsync(t => t.MaTaiLieu == id && t.MonHoc != null && t.MonHoc.MaNguoiDung == userId);

        if (taiLieu is null)
        {
            return NotFound();
        }

        try
        {
            var summary = await _geminiService.TomTatTaiLieuAsync(taiLieu.NoiDungTrichXuat);
            taiLieu.TomTatAI = summary;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã tạo tóm tắt tài liệu bằng Gemini AI thành công.";
        }
        catch (GeminiApiException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong muốn khi tóm tắt tài liệu {Id}", id);
            TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tạo tóm tắt AI. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int? returnMonHocId)
    {
        var userId = GetCurrentUserId();
        var taiLieu = await _context.TaiLieus
            .Include(t => t.MonHoc)
            .FirstOrDefaultAsync(t => t.MaTaiLieu == id && t.MonHoc != null && t.MonHoc.MaNguoiDung == userId);

        if (taiLieu is null)
        {
            return NotFound();
        }

        // 1. Gỡ khóa ngoại Restrict từ KeHoachHocTap -> TaiLieu
        var keHoachs = await _context.KeHoachHocTaps
            .Where(k => k.MaTaiLieu == id)
            .ToListAsync();
        foreach (var kh in keHoachs)
        {
            kh.MaTaiLieu = null;
        }

        // 2. Xóa các bản ghi con của DeThi (tránh Restrict ChiTietTraLoi -> CauHoi)
        var deThis = await _context.DeThis
            .Where(d => d.MaTaiLieu == id)
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

        var relativePath = taiLieu.DuongDanFile;
        var tieuDe = taiLieu.TieuDe;
        _context.TaiLieus.Remove(taiLieu);
        await _context.SaveChangesAsync();

        DeletePhysicalFileSafely(relativePath);

        TempData["SuccessMessage"] = $"Đã xóa tài liệu \"{tieuDe}\".";

        if (returnMonHocId.HasValue)
        {
            return RedirectToAction("Details", "MonHoc", new { id = returnMonHocId.Value });
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> GetMonHocSelectListAsync(string userId, int? selectedId)
    {
        return await _context.MonHocs
            .AsNoTracking()
            .Where(m => m.MaNguoiDung == userId)
            .OrderBy(m => m.TenMonHoc)
            .Select(m => new SelectListItem
            {
                Value = m.MaMonHoc.ToString(),
                Text = m.TenMonHoc,
                Selected = selectedId == m.MaMonHoc
            })
            .ToListAsync();
    }

    private void DeletePhysicalFileSafely(string relativePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

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
            _logger.LogWarning(ex, "Không thể xóa tệp vật lý {Path}", relativePath);
        }
    }
}
