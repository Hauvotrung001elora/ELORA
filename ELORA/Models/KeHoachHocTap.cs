using System.ComponentModel.DataAnnotations;

namespace StudyFlowAI.Models;

public class KeHoachHocTap
{
    [Key]
    public int MaKeHoach { get; set; }

    public int MaMonHoc { get; set; }

    public int? MaTaiLieu { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề buổi học.")]
    [StringLength(255)]
    public string TieuDe { get; set; } = string.Empty;

    public DateTime NgayHoc { get; set; }

    public TimeSpan GioBatDau { get; set; }

    [Range(5, 480)]
    public int ThoiLuongPhut { get; set; } = 60;

    /// <summary>
    /// Giá trị hợp lệ: "ChuaLam", "HoanThanh", "BoLo"
    /// </summary>
    [Required]
    [StringLength(30)]
    public string TrangThai { get; set; } = "ChuaLam";

    public bool DoAITao { get; set; }

    public MonHoc? MonHoc { get; set; }
    public TaiLieu? TaiLieu { get; set; }
    public ICollection<PhienPomodoro> PhienPomodoros { get; set; } = new List<PhienPomodoro>();
}
