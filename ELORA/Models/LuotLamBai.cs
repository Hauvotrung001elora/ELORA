using System.ComponentModel.DataAnnotations;

namespace StudyFlowAI.Models;

public class LuotLamBai
{
    [Key]
    public int MaLuotLam { get; set; }

    public int MaDeThi { get; set; }

    [Required]
    public string MaNguoiDung { get; set; } = string.Empty;

    public double DiemSo { get; set; }

    public int SoCauDung { get; set; }

    public int TongSoCau { get; set; }

    public DateTime ThoiGianBatDau { get; set; } = DateTime.Now;

    public DateTime? ThoiGianNopBai { get; set; }

    public string? NhanXetAI { get; set; }

    public DeThi? DeThi { get; set; }
    public NguoiDung? NguoiDung { get; set; }
    public ICollection<ChiTietTraLoi> ChiTietTraLois { get; set; } = new List<ChiTietTraLoi>();
}
