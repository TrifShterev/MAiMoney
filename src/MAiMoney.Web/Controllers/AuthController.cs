using System.Security.Claims;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using MAiMoney.Services.Services;
using MAiMoney.Web.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace MAiMoney.Web.Controllers;

/// <summary>
/// Handles the login, register and logout form posts. Signing in has to happen in a real HTTP
/// request (not inside a Blazor circuit), so the account pages post their forms here.
/// </summary>
[Route("auth")]
public class AuthController(IUserService users, IAntiforgery antiforgery) : Controller
{
    private const string RegisterPath = "/account/register";

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromForm] string? email,
        [FromForm] string? password,
        [FromForm] bool rememberMe,
        [FromForm] string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BackWithError(AuthenticationSetup.LoginPath, "Your session expired. Please try again.", returnUrl);
        }

        var user = await users.ValidateCredentialsAsync(email ?? string.Empty, password ?? string.Empty, cancellationToken);
        if (user is null)
        {
            return BackWithError(AuthenticationSetup.LoginPath, "Invalid email or password.", returnUrl);
        }

        await SignInAsync(user, rememberMe);
        return RedirectToLocal(returnUrl);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromForm] string? email,
        [FromForm] string? displayName,
        [FromForm] string? password,
        [FromForm] string? confirmPassword,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BackWithError(RegisterPath, "Your session expired. Please try again.");
        }

        if (password != confirmPassword)
        {
            return BackWithError(RegisterPath, "The passwords don't match.");
        }

        try
        {
            var user = await users.RegisterAsync(
                email ?? string.Empty, displayName ?? string.Empty, password ?? string.Empty, cancellationToken);
            await SignInAsync(user, isPersistent: false);
            return LocalRedirect("~/");
        }
        catch (ServiceValidationException ex)
        {
            return BackWithError(RegisterPath, ex.Message);
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (await antiforgery.IsRequestValidAsync(HttpContext))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return LocalRedirect(AuthenticationSetup.LoginPath);
    }

    private Task SignInAsync(User user, bool isPersistent)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Email, user.Email)
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = isPersistent });
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect("~/");

    private IActionResult BackWithError(string path, string error, string? returnUrl = null)
    {
        var query = $"?error={Uri.EscapeDataString(error)}";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            query += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        return LocalRedirect(path + query);
    }
}
