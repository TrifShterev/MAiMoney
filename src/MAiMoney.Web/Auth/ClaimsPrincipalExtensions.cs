using System.Security.Claims;

namespace MAiMoney.Web.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The id of the signed-in user. Throws when nobody is signed in.</summary>
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user is signed in.");
}
