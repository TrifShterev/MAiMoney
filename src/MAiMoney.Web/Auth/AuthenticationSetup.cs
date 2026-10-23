using Microsoft.AspNetCore.Authentication.Cookies;

namespace MAiMoney.Web.Auth;

public static class AuthenticationSetup
{
    public const string LoginPath = "/account/login";

    /// <summary>Cookie authentication. Pages redirect to the login page; API calls get 401/403 instead.</summary>
    public static IServiceCollection AddMAiMoneyAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = LoginPath;
                options.AccessDeniedPath = LoginPath;
                options.Cookie.Name = "MAiMoney.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;

                options.Events.OnRedirectToLogin = context =>
                    RedirectOrStatus(context.HttpContext, context.RedirectUri, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context =>
                    RedirectOrStatus(context.HttpContext, context.RedirectUri, StatusCodes.Status403Forbidden);
            });

        services.AddAuthorization();
        services.AddCascadingAuthenticationState();
        services.AddScoped<CurrentUser>();
        return services;
    }

    public static bool IsApiRequest(this HttpRequest request) => request.Path.StartsWithSegments("/api");

    private static Task RedirectOrStatus(HttpContext context, string redirectUri, int statusCode)
    {
        if (context.Request.IsApiRequest())
        {
            context.Response.StatusCode = statusCode;
        }
        else
        {
            context.Response.Redirect(redirectUri);
        }

        return Task.CompletedTask;
    }
}
