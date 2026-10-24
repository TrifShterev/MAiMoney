using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using MAiMoney.Services.Services;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace MAiMoney.Services.Tests;

[Collection(MongoCollection.Name)]
public class UserServiceTests(MongoFixture fixture)
{
    [Fact]
    public async Task Register_SeedsDefaultCategories()
    {
        var userId = await fixture.NewUserAsync();

        var categories = await fixture.Categories.GetAllAsync(userId);

        Assert.Equal(["Food", "Rent", "Salary"], categories.Select(c => c.Name));
        Assert.Equal("Groceries", categories.Single(c => c.Name == "Food").Description);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmailIgnoringCase()
    {
        var email = MongoFixture.NewEmail("dup");
        await fixture.Users.RegisterAsync(email, "First", "password123");

        var ex = await Assert.ThrowsAsync<ServiceValidationException>(
            () => fixture.Users.RegisterAsync(email.ToUpperInvariant(), "Second", "password123"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Register_RejectsShortPassword()
    {
        await Assert.ThrowsAsync<ServiceValidationException>(
            () => fixture.Users.RegisterAsync(MongoFixture.NewEmail("short"), "Short", "1234567"));
    }

    [Fact]
    public async Task ValidateCredentials_ChecksPassword()
    {
        var email = MongoFixture.NewEmail("login");
        var registered = await fixture.Users.RegisterAsync(email, "Login", "password123");

        var ok = await fixture.Users.ValidateCredentialsAsync($"  {email.ToUpperInvariant()} ", "password123");
        var wrong = await fixture.Users.ValidateCredentialsAsync(email, "wrong-password");

        Assert.Equal(registered.Id, ok?.Id);
        Assert.Null(wrong);
    }

    [Fact]
    public async Task Register_NormalizesEmailAndNameAndHashesPassword()
    {
        var email = MongoFixture.NewEmail("Mixed.Case");

        var user = await fixture.Users.RegisterAsync($"  {email.ToUpperInvariant()} ", "  Jane Doe ", "password123");
        var stored = await fixture.Db.Users.Find(u => u.Id == user.Id).SingleAsync();

        Assert.Equal(email.ToLowerInvariant(), stored.Email);
        Assert.Equal("Jane Doe", stored.DisplayName);
        Assert.NotEmpty(stored.PasswordHash);
        Assert.DoesNotContain("password123", stored.PasswordHash);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("   ", "Name")]
    [InlineData("not-an-email", "Name")]
    [InlineData("valid@example.test", "")]
    [InlineData("valid@example.test", "   ")]
    [InlineData("valid@example.test", "A display name that is clearly longer than fifty characters")]
    public async Task Register_RejectsInvalidEmailOrName(string email, string displayName)
    {
        await Assert.ThrowsAsync<ServiceValidationException>(
            () => fixture.Users.RegisterAsync(email, displayName, "password123"));
    }

    [Fact]
    public async Task Register_DoesNotCreateUserWhenRejected()
    {
        var email = MongoFixture.NewEmail("rejected");

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Users.RegisterAsync(email, "Name", "short"));

        Assert.Null(await fixture.Users.ValidateCredentialsAsync(email, "short"));
    }

    [Theory]
    [InlineData("", "password123")]
    [InlineData("   ", "password123")]
    [InlineData("unknown@example.test", "password123")]
    [InlineData("known", "")]
    public async Task ValidateCredentials_ReturnsNullForUnknownEmailOrBlankInput(string email, string password)
    {
        var knownEmail = MongoFixture.NewEmail("known");
        await fixture.Users.RegisterAsync(knownEmail, "Known", "password123");

        var user = await fixture.Users.ValidateCredentialsAsync(email == "known" ? knownEmail : email, password);

        Assert.Null(user);
    }

    [Fact]
    public async Task ValidateCredentials_StoresNewHashWhenRehashIsNeeded()
    {
        var hasher = new VersionedPasswordHasher();
        var users = new UserService(fixture.Db, hasher, fixture.Categories);
        var email = MongoFixture.NewEmail("rehash");
        var registered = await users.RegisterAsync(email, "Rehash", "password123");
        Assert.Equal("v1:password123", registered.PasswordHash);

        hasher.Version = 2;
        var user = await users.ValidateCredentialsAsync(email, "password123");

        Assert.Equal("v2:password123", user?.PasswordHash);
        var stored = await fixture.Db.Users.Find(u => u.Id == registered.Id).SingleAsync();
        Assert.Equal("v2:password123", stored.PasswordHash);
    }

    /// <summary>Readable fake hasher that asks for a rehash when a hash is from an older version.</summary>
    private sealed class VersionedPasswordHasher : IPasswordHasher<User>
    {
        public int Version { get; set; } = 1;

        public string HashPassword(User user, string password) => $"v{Version}:{password}";

        public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
        {
            var parts = hashedPassword.Split(':', 2);
            if (parts[1] != providedPassword)
            {
                return PasswordVerificationResult.Failed;
            }

            return parts[0] == $"v{Version}"
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
