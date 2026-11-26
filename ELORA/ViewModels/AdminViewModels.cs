namespace StudyFlowAI.ViewModels;

public class AdminDashboardViewModel
{
    public int TongNguoiDung { get; set; }
    public int SoTaiKhoanBiKhoa { get; set; }
    public int TongMonHoc { get; set; }
    public int TongTaiLieu { get; set; }
    public int TongDeThi { get; set; }
    public int TongLuotLamBai { get; set; }
    public double? DiemTrungBinhHeThong { get; set; }
    public int TongPhienPomodoro { get; set; }
    public int TongPhutPomodoro { get; set; }

    public List<AdminTopUserItem> TopNguoiDung { get; set; } = new();
    public List<AdminRecentDocumentItem> TaiLieuGanDay { get; set; } = new();
    public List<AdminRecentQuizItem> DeThiGanDay { get; set; } = new();
}

public class AdminTopUserItem
{
    public string Id { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int SoMonHoc { get; set; }
    public int SoTaiLieu { get; set; }
    public int SoLuotLamBai { get; set; }
    public int SoPhienPomodoro { get; set; }
    public int TongHoatDong => SoMonHoc + SoTaiLieu + SoLuotLamBai + SoPhienPomodoro;
}

public class AdminRecentDocumentItem
{
    public int MaTaiLieu { get; set; }
    public string TieuDe { get; set; } = string.Empty;
    public string TenMonHoc { get; set; } = string.Empty;
    public string MaMau { get; set; } = "#4f46e5";
    public string NguoiTao { get; set; } = string.Empty;
    public bool DaTomTatAI { get; set; }
    public DateTime NgayTaiLen { get; set; }
}

public class AdminRecentQuizItem
{
    public int MaDeThi { get; set; }
    public string TieuDe { get; set; } = string.Empty;
    public string TenMonHoc { get; set; } = string.Empty;
    public string NguoiTao { get; set; } = string.Empty;
    public string DoKho { get; set; } = "TrungBinh";
    public int SoCauHoi { get; set; }
    public int SoLuotLam { get; set; }
    public DateTime NgayTao { get; set; }
}

public class AdminUsersViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int TongTaiKhoan { get; set; }
    public int SoDangHoatDong { get; set; }
    public int SoDangBiKhoa { get; set; }
    public List<AdminUserRowViewModel> Users { get; set; } = new();
}

public class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime NgayTao { get; set; }
    public int SoMonHoc { get; set; }
    public int SoTaiLieu { get; set; }
    public bool IsLockedOut { get; set; }
    public bool IsCurrentUser { get; set; }
}
