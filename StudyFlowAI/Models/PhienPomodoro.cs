using System.ComponentModel.DataAnnotations;

namespace StudyFlowAI.Models;

public class PhienPomodoro
{
    [Key]
    public int MaPhien { get; set; }

    [Required]
    public string MaNguoiDung { get; set; } = string.Empty;

    public int? MaKeHoach { get; set; }

    [Range(1, 240)]
    public int ThoiLuongPhut { get; set; } = 25;

    public DateTime ThoiDiemHoanThanh { get; set; } = DateTime.Now;

    public NguoiDung? NguoiDung { get; set; }
    public KeHoachHocTap? KeHoachHocTap { get; set; }
}
