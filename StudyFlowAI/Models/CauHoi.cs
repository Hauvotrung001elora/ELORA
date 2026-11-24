using System.ComponentModel.DataAnnotations;

namespace StudyFlowAI.Models;

public class CauHoi
{
    [Key]
    public int MaCauHoi { get; set; }

    public int MaDeThi { get; set; }

    [Required]
    public string NoiDung { get; set; } = string.Empty;

    [Required]
    public string DapAnA { get; set; } = string.Empty;

    [Required]
    public string DapAnB { get; set; } = string.Empty;

    [Required]
    public string DapAnC { get; set; } = string.Empty;

    [Required]
    public string DapAnD { get; set; } = string.Empty;

    /// <summary>
    /// Giá trị hợp lệ: "A", "B", "C", "D"
    /// </summary>
    [Required]
    [StringLength(5)]
    public string DapAnDung { get; set; } = "A";

    public string GiaiThichAI { get; set; } = string.Empty;

    public DeThi? DeThi { get; set; }
    public ICollection<ChiTietTraLoi> ChiTietTraLois { get; set; } = new List<ChiTietTraLoi>();
}
