using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyFlowAI.Models;

namespace StudyFlowAI.Data;

public static class DbSeeder
{
    public const string RoleAdmin = "Admin";
    public const string RoleSinhVien = "SinhVien";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        try
        {
            var context = provider.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();

            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = provider.GetRequiredService<UserManager<NguoiDung>>();

            string[] roles = [RoleAdmin, RoleSinhVien];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            const string adminEmail = "admin@elora.vn";
            const string legacyAdminEmail = "admin@studyflow.vn";

            var adminUser = await userManager.FindByEmailAsync(adminEmail)
                            ?? await userManager.FindByEmailAsync(legacyAdminEmail);

            if (adminUser is null)
            {
                adminUser = new NguoiDung
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    HoTen = "Quản trị viên ELORA",
                    NgayTao = DateTime.Now
                };

                var createResult = await userManager.CreateAsync(adminUser, "Admin@123");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, RoleAdmin);
                }
                else
                {
                    logger.LogWarning("Không thể tạo tài khoản Admin mặc định: {Errors}",
                        string.Join("; ", createResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                if (!string.Equals(adminUser.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
                {
                    await userManager.SetEmailAsync(adminUser, adminEmail);
                    await userManager.SetUserNameAsync(adminUser, adminEmail);
                }

                var needUpdateProfile = false;
                if (!adminUser.EmailConfirmed)
                {
                    adminUser.EmailConfirmed = true;
                    needUpdateProfile = true;
                }

                if (string.IsNullOrWhiteSpace(adminUser.HoTen) ||
                    string.Equals(adminUser.HoTen, "Quản trị viên StudyFlow AI", StringComparison.OrdinalIgnoreCase))
                {
                    adminUser.HoTen = "Quản trị viên ELORA";
                    needUpdateProfile = true;
                }

                if (needUpdateProfile)
                {
                    await userManager.UpdateAsync(adminUser);
                }

                if (!await userManager.IsInRoleAsync(adminUser, RoleAdmin))
                {
                    await userManager.AddToRoleAsync(adminUser, RoleAdmin);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi chạy migration/seed dữ liệu ban đầu. Hãy kiểm tra ConnectionString SQL Server.");
        }
    }
}
