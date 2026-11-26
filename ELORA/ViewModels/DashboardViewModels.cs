namespace StudyFlowAI.ViewModels;

public class DashboardViewModel
{
    public string HoTen { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    // KPI tổng quan
    public int TongMonHoc { get; set; }
    public int TongTaiLieu { get; set; }
    public double? DiemTrungBinh { get; set; }
    public int SoLuotLamBai { get; set; }
    public int TongPhutPomodoroTuanNay { get; set; }
    public int SoPhienPomodoroTuanNay { get; set; }
    public double TyLeHoanThanhKeHoach { get; set; }

    // Thống kê chi tiết kế hoạch
    public int KeHoachHoanThanh { get; set; }
    public int KeHoachChuaLam { get; set; }
    public int KeHoachBoLo { get; set; }
    public int TongKeHoach { get; set; }

    // Góc nhắc nhở hôm nay
    public List<KeHoachHomNayItem> BuoiHocHomNay { get; set; } = new();
    public List<MonSapThiItem> MonSapThi { get; set; } = new();

    // Cờ trạng thái dữ liệu cho 3 biểu đồ Chart.js
    public bool HasScoreData { get; set; }
    public bool HasPomodoroData { get; set; }
    public bool HasPlanData { get; set; }
    public int TongPhutPomodoro7Ngay { get; set; }

    // JSON serialized an toàn cho Chart.js
    public string ChartDataJson { get; set; } = "{}";
}

public class KeHoachHomNayItem
{
    public int MaKeHoach { get; set; }
    public string TieuDe { get; set; } = string.Empty;
    public string TenMonHoc { get; set; } = string.Empty;
    public string MaMau { get; set; } = "#4f46e5";
    public TimeSpan GioBatDau { get; set; }
    public int ThoiLuongPhut { get; set; }
    public bool DoAITao { get; set; }
}

public class MonSapThiItem
{
    public int MaMonHoc { get; set; }
    public string TenMonHoc { get; set; } = string.Empty;
    public string MaMau { get; set; } = "#4f46e5";
    public DateTime NgayThi { get; set; }
    public int SoNgayConLai { get; set; }
    public double? DiemMucTieu { get; set; }
    public int DoKho { get; set; }
}

public class DashboardChartPayload
{
    public List<string> ScoreLabels { get; set; } = new();
    public List<double> ScoreValues { get; set; } = new();
    public List<string> ScoreTooltips { get; set; } = new();

    public List<string> PomodoroLabels { get; set; } = new();
    public List<int> PomodoroMinutes { get; set; } = new();
    public List<int> PomodoroSessions { get; set; } = new();

    public List<string> PlanLabels { get; set; } = ["Hoàn thành", "Chưa làm", "Bỏ lỡ"];
    public List<int> PlanValues { get; set; } = [0, 0, 0];
}
