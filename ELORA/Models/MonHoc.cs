using System.ComponentModel.DataAnnotations;

namespace StudyFlowAI.Models;

public class MonHoc
{
    [Key]
    public int MaMonHoc { get; set; }

    [Required]
    public string MaNguoiDung { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên môn học.")]
    [StringLength(200)]
    public string TenMonHoc { get; set; } = string.Empty;

    public DateTime? NgayThi { get; set; }

    [Range(0, 10, ErrorMessage = "Điểm mục tiêu phải từ 0 đến 10.")]
    public double? DiemMucTieu { get; set; }

    [Range(1, 3, ErrorMessage = "Độ khó phải từ 1 đến 3.")]
    public int DoKho { get; set; } = 2;

    [Required]
    [StringLength(20)]
    public string MaMau { get; set; } = "#3b82f6";

    public DateTime NgayTao { get; set; } = DateTime.Now;

    public NguoiDung? NguoiDung { get; set; }
    public ICollection<TaiLieu> TaiLieus { get; set; } = new List<TaiLieu>();
    public ICollection<KeHoachHocTap> KeHoachHocTaps { get; set; } = new List<KeHoachHocTap>();
}
