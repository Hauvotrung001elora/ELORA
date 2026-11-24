using System.ComponentModel.DataAnnotations;
using StudyFlowAI.Models;

namespace StudyFlowAI.ViewModels;

public class KeHoachHocTapIndexViewModel
{
    public List<MonHoc> MonHocs { get; set; } = new();
    public List<TaiLieu> TaiLieus { get; set; } = new();
    public Dictionary<int, double> DiemTrungBinhTheoMon { get; set; } = new();
    public int SoBuoiChuaLam { get; set; }
    public int SoBuoiHoanThanh { get; set; }
    public int SoBuoiBoLo { get; set; }
    public int SoBaiThiDiemThap { get; set; }
    public bool IsGeminiConfigured { get; set; }
    public string? PrefilledKhungGioRanh { get; set; }
    public bool AutoOpenGenerateModal { get; set; }
}

public class KeHoachManualFormModel
{
    public int MaKeHoach { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn môn học.")]
    public int MaMonHoc { get; set; }

    public int? MaTaiLieu { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề buổi học.")]
    [StringLength(255)]
    public string TieuDe { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn ngày học.")]
    [DataType(DataType.Date)]
    public DateTime NgayHoc { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    public TimeSpan GioBatDau { get; set; } = new(19, 30, 0);

    [Range(5, 480, ErrorMessage = "Thời lượng phải từ 5 đến 480 phút.")]
    public int ThoiLuongPhut { get; set; } = 60;

    public string TrangThai { get; set; } = "ChuaLam";
}

public class TaoLichAIInputModel
{
    [Required(ErrorMessage = "Vui lòng chọn ít nhất một môn học.")]
    public List<int> SelectedMonHocIds { get; set; } = new();

    [Required]
    [DataType(DataType.Date)]
    public DateTime NgayBatDau { get; set; } = DateTime.Today;

    [Range(1, 30, ErrorMessage = "Số ngày lập lịch từ 1 đến 30 ngày.")]
    public int SoNgay { get; set; } = 7;

    [Required(ErrorMessage = "Vui lòng mô tả lịch bận / khung giờ rảnh của bạn.")]
    [StringLength(2000)]
    public string KhungGioRanh { get; set; } = "Thứ 2 - Thứ 6: 19:30 - 21:30; Thứ 7 - CN: 09:00 - 11:30";
}

public class DieuChinhLichAIInputModel
{
    [StringLength(2000)]
    public string KhungGioRanh { get; set; } = "Thứ 2 - Thứ 6: 19:30 - 21:30; Thứ 7 - CN: 09:00 - 11:30";

    public bool XoaBuoiBoLoCu { get; set; } = false;
}
