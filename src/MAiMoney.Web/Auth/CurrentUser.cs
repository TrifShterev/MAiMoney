using Microsoft.AspNetCore.Components.Authorization;

namespace MAiMoney.Web.Auth;

/// <summary>Gives Blazor components the id of the signed-in user.</summary>
public class CurrentUser(AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<string> GetIdAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.GetUserId();
    }
}
