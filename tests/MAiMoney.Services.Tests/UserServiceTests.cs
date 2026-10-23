using MAiMoney.Services.Exceptions;

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
        var email = $"dup-{Guid.NewGuid():N}@example.test";
        await fixture.Users.RegisterAsync(email, "First", "password123");

        var ex = await Assert.ThrowsAsync<ServiceValidationException>(
            () => fixture.Users.RegisterAsync(email.ToUpperInvariant(), "Second", "password123"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Register_RejectsShortPassword()
    {
        await Assert.ThrowsAsync<ServiceValidationException>(
            () => fixture.Users.RegisterAsync($"short-{Guid.NewGuid():N}@example.test", "Short", "1234567"));
    }

    [Fact]
    public async Task ValidateCredentials_ChecksPassword()
    {
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var registered = await fixture.Users.RegisterAsync(email, "Login", "password123");

        var ok = await fixture.Users.ValidateCredentialsAsync($"  {email.ToUpperInvariant()} ", "password123");
        var wrong = await fixture.Users.ValidateCredentialsAsync(email, "wrong-password");

        Assert.Equal(registered.Id, ok?.Id);
        Assert.Null(wrong);
    }
}
