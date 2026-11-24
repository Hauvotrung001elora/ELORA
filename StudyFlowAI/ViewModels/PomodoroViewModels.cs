using System.ComponentModel.DataAnnotations;
using StudyFlowAI.Models;

namespace StudyFlowAI.ViewModels;

public class PomodoroIndexViewModel
{
    public int? SelectedMaKeHoach { get; set; }
    public List<KeHoachHocTap> BuoiHocChuaLam { get; set; } = new();
    public List<PhienPomodoro> LichSuGanDay { get; set; } = new();
    public int SoPhienHomNay { get; set; }
    public int TongPhutHomNay { get; set; }
    public int SoPhienTuanNay { get; set; }
    public int TongPhutTuanNay { get; set; }
}

public class LuuPhienPomodoroRequest
{
    public int? MaKeHoach { get; set; }

    [Range(1, 240)]
    public int ThoiLuongPhut { get; set; } = 25;

    public bool DanhDauHoanThanh { get; set; } = false;
}
