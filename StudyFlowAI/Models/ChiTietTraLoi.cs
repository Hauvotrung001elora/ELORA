using System.ComponentModel.DataAnnotations;

namespace StudyFlowAI.Models;

public class ChiTietTraLoi
{
    [Key]
    public int MaChiTiet { get; set; }

    public int MaLuotLam { get; set; }

    public int MaCauHoi { get; set; }

    [StringLength(5)]
    public string? DapAnChon { get; set; }

    public bool LaDapAnDung { get; set; }

    public LuotLamBai? LuotLamBai { get; set; }
    public CauHoi? CauHoi { get; set; }
}
