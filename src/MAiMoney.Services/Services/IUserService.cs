using MAiMoney.Services.Entities;

namespace MAiMoney.Services.Services;

public interface IUserService
{
    /// <summary>Creates the account and seeds the default categories.</summary>
    Task<User> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken = default);

    /// <summary>Returns the user when the email and password match, otherwise null.</summary>
    Task<User?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);
}
