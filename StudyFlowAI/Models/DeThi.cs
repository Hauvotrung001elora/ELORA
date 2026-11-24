using System.ComponentModel.DataAnnotations;

namespace StudyFlowAI.Models;

public class DeThi
{
    [Key]
    public int MaDeThi { get; set; }

    public int MaTaiLieu { get; set; }

    [Required]
    [StringLength(255)]
    public string TieuDe { get; set; } = string.Empty;

    /// <summary>
    /// Giá trị hợp lệ: "De", "TrungBinh", "Kho"
    /// </summary>
    [Required]
    [StringLength(30)]
    public string DoKho { get; set; } = "TrungBinh";

    [Range(1, 300)]
    public int ThoiGianLamPhut { get; set; } = 15;

    public DateTime NgayTao { get; set; } = DateTime.Now;

    public TaiLieu? TaiLieu { get; set; }
    public ICollection<CauHoi> CauHois { get; set; } = new List<CauHoi>();
    public ICollection<LuotLamBai> LuotLamBais { get; set; } = new List<LuotLamBai>();
}
