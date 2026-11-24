using System.ComponentModel.DataAnnotations;

namespace StudyFlowAI.Models;

public class TaiLieu
{
    [Key]
    public int MaTaiLieu { get; set; }

    public int MaMonHoc { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề tài liệu.")]
    [StringLength(255)]
    public string TieuDe { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string DuongDanFile { get; set; } = string.Empty;

    public string NoiDungTrichXuat { get; set; } = string.Empty;

    public string? TomTatAI { get; set; }

    public DateTime NgayTaiLen { get; set; } = DateTime.Now;

    public MonHoc? MonHoc { get; set; }
    public ICollection<KeHoachHocTap> KeHoachHocTaps { get; set; } = new List<KeHoachHocTap>();
    public ICollection<DeThi> DeThis { get; set; } = new List<DeThi>();
}
