using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using StudyFlowAI.Models;

namespace StudyFlowAI.ViewModels;

public class DeThiIndexViewModel
{
    public List<DeThi> DeThis { get; set; } = new();
    public int TongLuotLam { get; set; }
    public double? DiemTrungBinh { get; set; }
    public bool IsGeminiConfigured { get; set; }
}

public class TaoDeThiViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn tài liệu nguồn.")]
    [Display(Name = "Tài liệu nguồn")]
    public int MaTaiLieu { get; set; }

    [StringLength(255, ErrorMessage = "Tiêu đề đề thi tối đa 255 ký tự.")]
    [Display(Name = "Tên đề thi (tuỳ chọn)")]
    public string? TieuDe { get; set; }

    [Required]
    [Range(5, 20, ErrorMessage = "Số câu hỏi hợp lệ là 5, 10, 15 hoặc 20.")]
    [Display(Name = "Số lượng câu hỏi")]
    public int SoCau { get; set; } = 10;

    [Required]
    [Display(Name = "Độ khó")]
    public string DoKho { get; set; } = "TrungBinh";

    [Required]
    [Range(1, 180, ErrorMessage = "Thời gian làm bài từ 1 đến 180 phút.")]
    [Display(Name = "Thời gian làm bài (phút)")]
    public int ThoiGianLamPhut { get; set; } = 15;

    public bool IsGeminiConfigured { get; set; }
    public List<SelectListItem> TaiLieuOptions { get; set; } = new();
}

public class LamBaiViewModel
{
    public DeThi DeThi { get; set; } = null!;
    public List<CauHoi> CauHois { get; set; } = new();
    public DateTime ThoiGianBatDau { get; set; } = DateTime.Now;
}

public class NopBaiInputModel
{
    public int MaDeThi { get; set; }
    public DateTime ThoiGianBatDau { get; set; }
    public Dictionary<int, string> TraLois { get; set; } = new();
}

public class KetQuaBaiThiViewModel
{
    public LuotLamBai LuotLamBai { get; set; } = null!;
    public List<ChiTietTraLoi> ChiTietTraLois { get; set; } = new();

    public TimeSpan? ThoiGianThucHien => LuotLamBai.ThoiGianNopBai.HasValue
        ? LuotLamBai.ThoiGianNopBai.Value - LuotLamBai.ThoiGianBatDau
        : null;
}

public class LichSuLamBaiViewModel
{
    public List<LuotLamBai> LuotLamBais { get; set; } = new();
    public double? DiemTrungBinh { get; set; }
    public double? DiemCaoNhat { get; set; }
}
