using System.ComponentModel.DataAnnotations;
using StudyFlowAI.Models;

namespace StudyFlowAI.ViewModels;

public class MonHocFormViewModel
{
    public int MaMonHoc { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên môn học.")]
    [StringLength(200, ErrorMessage = "Tên môn học tối đa 200 ký tự.")]
    [Display(Name = "Tên môn học")]
    public string TenMonHoc { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Ngày thi dự kiến")]
    public DateTime? NgayThi { get; set; }

    [Range(0, 10, ErrorMessage = "Điểm mục tiêu phải từ 0 đến 10.")]
    [Display(Name = "Điểm mục tiêu (thang 10)")]
    public double? DiemMucTieu { get; set; }

    [Range(1, 3, ErrorMessage = "Độ khó phải từ 1 đến 3.")]
    [Display(Name = "Độ khó")]
    public int DoKho { get; set; } = 2;

    [Required(ErrorMessage = "Vui lòng chọn màu đại diện.")]
    [StringLength(20)]
    [Display(Name = "Màu đại diện")]
    public string MaMau { get; set; } = "#4f46e5";
}

public class MonHocCardViewModel
{
    public int MaMonHoc { get; set; }
    public string TenMonHoc { get; set; } = string.Empty;
    public DateTime? NgayThi { get; set; }
    public double? DiemMucTieu { get; set; }
    public int DoKho { get; set; }
    public string MaMau { get; set; } = "#4f46e5";
    public DateTime NgayTao { get; set; }
    public int SoTaiLieu { get; set; }
    public int SoDeThi { get; set; }

    public int? SoNgayConLai => NgayThi.HasValue
        ? (int)(NgayThi.Value.Date - DateTime.Today).TotalDays
        : null;

    public string DoKhoText => DoKho switch
    {
        1 => "Dễ",
        3 => "Khó",
        _ => "Trung bình"
    };
}

public class MonHocDetailsViewModel
{
    public MonHoc MonHoc { get; set; } = null!;
    public int? SoNgayConLai => MonHoc.NgayThi.HasValue
        ? (int)(MonHoc.NgayThi.Value.Date - DateTime.Today).TotalDays
        : null;
    public string DoKhoText => MonHoc.DoKho switch
    {
        1 => "Dễ",
        3 => "Khó",
        _ => "Trung bình"
    };
    public List<TaiLieu> TaiLieus { get; set; } = new();
    public int TongSoDeThi { get; set; }
}

public class ScanTkbViewModel
{
    public bool IsGeminiConfigured { get; set; }
    public bool HasResult { get; set; }
    public string? UploadedFileName { get; set; }
    public string? ImagePreviewDataUrl { get; set; }
    public string LichBanTuTkb { get; set; } = string.Empty;
    public string? LichCaNhanBoSung { get; set; }
    public List<ScanTkbSubjectItemViewModel> Subjects { get; set; } = new();
}

public class ScanTkbSubjectItemViewModel
{
    public bool Chon { get; set; } = true;

    [StringLength(200, ErrorMessage = "Tên môn học tối đa 200 ký tự.")]
    public string TenMonHoc { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? NgayThi { get; set; }

    [Range(0, 10, ErrorMessage = "Điểm mục tiêu phải từ 0 đến 10.")]
    public double? DiemMucTieu { get; set; }

    [Range(1, 3, ErrorMessage = "Độ khó phải từ 1 đến 3.")]
    public int DoKho { get; set; } = 2;

    [StringLength(20)]
    public string MaMau { get; set; } = "#4f46e5";

    public bool DaTonTai { get; set; }
}

public class ConfirmScanTkbInputModel
{
    public List<ScanTkbSubjectItemViewModel> Subjects { get; set; } = new();

    [StringLength(1500)]
    public string? LichBanTuTkb { get; set; }

    [StringLength(800)]
    public string? LichCaNhanBoSung { get; set; }

    public bool DiToiLapLichAI { get; set; }
}
