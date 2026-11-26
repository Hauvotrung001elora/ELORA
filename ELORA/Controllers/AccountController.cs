using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudyFlowAI.Data;
using StudyFlowAI.Models;
using StudyFlowAI.ViewModels;

namespace StudyFlowAI.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<NguoiDung> _userManager;
    private readonly SignInManager<NguoiDung> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<NguoiDung> userManager,
        SignInManager<NguoiDung> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Người dùng {Email} đã đăng nhập thành công.", email);
            TempData["SuccessMessage"] = $"Chào mừng {user.HoTen} quay trở lại ELORA!";

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            var isAdminLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow.AddHours(1);
            ModelState.AddModelError(
                string.Empty,
                isAdminLocked
                    ? "Tài khoản của bạn đã bị quản trị viên khóa. Vui lòng liên hệ quản trị viên để được hỗ trợ."
                    : "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 5 phút.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã được đăng ký. Vui lòng sử dụng email khác.");
            return View(model);
        }

        var user = new NguoiDung
        {
            UserName = email,
            Email = email,
            HoTen = model.HoTen.Trim(),
            NgayTao = DateTime.Now
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (createResult.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, DbSeeder.RoleSinhVien);
            await _signInManager.SignInAsync(user, isPersistent: false);

            _logger.LogInformation("Tài khoản sinh viên mới {Email} đã được tạo.", email);
            TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Bắt đầu hành trình học tập cùng ELORA.";

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        foreach (var error in createResult.Errors)
        {
            ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        TempData["SuccessMessage"] = "Bạn đã đăng xuất thành công.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private static string TranslateIdentityError(IdentityError error) => error.Code switch
    {
        "DuplicateEmail" or "DuplicateUserName" => "Email này đã tồn tại trong hệ thống.",
        "PasswordTooShort" => "Mật khẩu phải có ít nhất 6 ký tự.",
        "PasswordRequiresDigit" => "Mật khẩu phải chứa ít nhất một chữ số (0-9).",
        "PasswordRequiresLower" => "Mật khẩu phải chứa ít nhất một chữ thường (a-z).",
        "PasswordRequiresUpper" => "Mật khẩu phải chứa ít nhất một chữ in hoa (A-Z).",
        "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải chứa ít nhất một ký tự đặc biệt.",
        _ => error.Description
    };
}
