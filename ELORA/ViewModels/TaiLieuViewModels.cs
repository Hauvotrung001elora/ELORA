using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using StudyFlowAI.Models;

namespace StudyFlowAI.ViewModels;

public class TaiLieuIndexViewModel
{
    public int? FilterMaMonHoc { get; set; }
    public List<SelectListItem> MonHocOptions { get; set; } = new();
    public List<TaiLieu> TaiLieus { get; set; } = new();
}

public class TaiLieuUploadViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn môn học.")]
    [Display(Name = "Môn học")]
    public int MaMonHoc { get; set; }

    [StringLength(255, ErrorMessage = "Tiêu đề tối đa 255 ký tự.")]
    [Display(Name = "Tiêu đề tài liệu")]
    public string? TieuDe { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn tệp tài liệu (.pdf, .docx, .txt).")]
    [Display(Name = "Tệp tài liệu")]
    public IFormFile? TepTaiLieu { get; set; }

    public List<SelectListItem> MonHocOptions { get; set; } = new();
}

public class TaiLieuDetailsViewModel
{
    public TaiLieu TaiLieu { get; set; } = null!;
    public string TenFileGoc { get; set; } = string.Empty;
    public string DinhDangFile { get; set; } = string.Empty;
    public int SoKyTuTrichXuat { get; set; }
    public int SoTuUocTinh { get; set; }
    public bool IsGeminiConfigured { get; set; }
    public List<DeThi> DeThis { get; set; } = new();
}
