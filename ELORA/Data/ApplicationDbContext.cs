using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyFlowAI.Models;

namespace StudyFlowAI.Data;

public class ApplicationDbContext : IdentityDbContext<NguoiDung>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MonHoc> MonHocs => Set<MonHoc>();
    public DbSet<TaiLieu> TaiLieus => Set<TaiLieu>();
    public DbSet<KeHoachHocTap> KeHoachHocTaps => Set<KeHoachHocTap>();
    public DbSet<PhienPomodoro> PhienPomodoros => Set<PhienPomodoro>();
    public DbSet<DeThi> DeThis => Set<DeThi>();
    public DbSet<CauHoi> CauHois => Set<CauHoi>();
    public DbSet<LuotLamBai> LuotLamBais => Set<LuotLamBai>();
    public DbSet<ChiTietTraLoi> ChiTietTraLois => Set<ChiTietTraLoi>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<NguoiDung>(entity =>
        {
            entity.Property(e => e.HoTen).HasDefaultValue(string.Empty);
        });

        builder.Entity<MonHoc>(entity =>
        {
            entity.ToTable("MonHoc");
            entity.Property(e => e.MaMau).HasDefaultValue("#3b82f6");
            entity.HasOne(e => e.NguoiDung)
                  .WithMany(u => u.MonHocs)
                  .HasForeignKey(e => e.MaNguoiDung)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TaiLieu>(entity =>
        {
            entity.ToTable("TaiLieu");
            entity.HasOne(e => e.MonHoc)
                  .WithMany(m => m.TaiLieus)
                  .HasForeignKey(e => e.MaMonHoc)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<KeHoachHocTap>(entity =>
        {
            entity.ToTable("KeHoachHocTap");
            entity.Property(e => e.TrangThai).HasDefaultValue("ChuaLam");
            entity.HasOne(e => e.MonHoc)
                  .WithMany(m => m.KeHoachHocTaps)
                  .HasForeignKey(e => e.MaMonHoc)
                  .OnDelete(DeleteBehavior.Cascade);

            // Tránh lỗi Multiple Cascade Paths trên SQL Server (MonHoc -> TaiLieu -> KeHoachHocTap)
            entity.HasOne(e => e.TaiLieu)
                  .WithMany(t => t.KeHoachHocTaps)
                  .HasForeignKey(e => e.MaTaiLieu)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PhienPomodoro>(entity =>
        {
            entity.ToTable("PhienPomodoro");

            // Tránh lỗi Multiple Cascade Paths trên SQL Server
            entity.HasOne(e => e.NguoiDung)
                  .WithMany(u => u.PhienPomodoros)
                  .HasForeignKey(e => e.MaNguoiDung)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.KeHoachHocTap)
                  .WithMany(k => k.PhienPomodoros)
                  .HasForeignKey(e => e.MaKeHoach)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DeThi>(entity =>
        {
            entity.ToTable("DeThi");
            entity.Property(e => e.DoKho).HasDefaultValue("TrungBinh");
            entity.HasOne(e => e.TaiLieu)
                  .WithMany(t => t.DeThis)
                  .HasForeignKey(e => e.MaTaiLieu)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CauHoi>(entity =>
        {
            entity.ToTable("CauHoi");
            entity.HasOne(e => e.DeThi)
                  .WithMany(d => d.CauHois)
                  .HasForeignKey(e => e.MaDeThi)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LuotLamBai>(entity =>
        {
            entity.ToTable("LuotLamBai");
            entity.HasOne(e => e.DeThi)
                  .WithMany(d => d.LuotLamBais)
                  .HasForeignKey(e => e.MaDeThi)
                  .OnDelete(DeleteBehavior.Cascade);

            // Tránh lỗi Multiple Cascade Paths trên SQL Server (NguoiDung -> MonHoc -> TaiLieu -> DeThi -> LuotLamBai)
            entity.HasOne(e => e.NguoiDung)
                  .WithMany(u => u.LuotLamBais)
                  .HasForeignKey(e => e.MaNguoiDung)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChiTietTraLoi>(entity =>
        {
            entity.ToTable("ChiTietTraLoi");
            entity.HasOne(e => e.LuotLamBai)
                  .WithMany(l => l.ChiTietTraLois)
                  .HasForeignKey(e => e.MaLuotLam)
                  .OnDelete(DeleteBehavior.Cascade);

            // Tránh lỗi Multiple Cascade Paths trên SQL Server (DeThi -> CauHoi -> ChiTietTraLoi)
            entity.HasOne(e => e.CauHoi)
                  .WithMany(c => c.ChiTietTraLois)
                  .HasForeignKey(e => e.MaCauHoi)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
