using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace StudyFlowAI.Models;

public class NguoiDung : IdentityUser
{
    [Required]
    [StringLength(150)]
    public string HoTen { get; set; } = string.Empty;

    public DateTime NgayTao { get; set; } = DateTime.Now;

    public ICollection<MonHoc> MonHocs { get; set; } = new List<MonHoc>();
    public ICollection<PhienPomodoro> PhienPomodoros { get; set; } = new List<PhienPomodoro>();
    public ICollection<LuotLamBai> LuotLamBais { get; set; } = new List<LuotLamBai>();
}
