using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyFlowAI.Data;
using StudyFlowAI.Models;
using StudyFlowAI.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Không tìm thấy chuỗi kết nối 'DefaultConnection' trong appsettings.json.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentity<NguoiDung, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    // Kiểm tra SecurityStamp ngay lập tức thay vì chờ mặc định 30 phút
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);

    options.Events.OnValidatePrincipal = async context =>
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var lockState = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.LockoutEnabled, u.LockoutEnd })
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            var isLocked = lockState is null ||
                (lockState.LockoutEnabled &&
                 lockState.LockoutEnd.HasValue &&
                 lockState.LockoutEnd.Value > DateTimeOffset.UtcNow);

            if (isLocked)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                context.HttpContext.Items["AccountLockedOut"] = true;
                return;
            }
        }

        await SecurityStampValidator.ValidatePrincipalAsync(context);
    };
});

builder.Services.AddScoped<DocumentExtractorService>();
builder.Services.AddHttpClient<GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!EF.IsDesignTime)
{
    await DbSeeder.SeedAsync(app.Services);
}

var enableHttpsRedirection = builder.Configuration.GetValue<bool>("Hosting:EnableHttpsRedirection", false);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    if (enableHttpsRedirection)
    {
        app.UseHsts();
    }
}

if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Items.ContainsKey("AccountLockedOut"))
    {
        var isAjax = string.Equals(
                context.Request.Headers.XRequestedWith,
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase) ||
            context.Request.Headers.Accept.ToString()
                .Contains("application/json", StringComparison.OrdinalIgnoreCase);

        if (isAjax)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                locked = true,
                message = "Tài khoản của bạn đã bị quản trị viên khóa. Phiên đăng nhập hiện tại đã bị chấm dứt."
            });
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/Account/Login?locked=1");
            return;
        }
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
