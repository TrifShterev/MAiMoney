using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using MAiMoney.Services.Models;
using MongoDB.Bson;

namespace MAiMoney.Services.Tests;

[Collection(MongoCollection.Name)]
public class CategoryServiceTests(MongoFixture fixture)
{
    [Fact]
    public async Task Create_TrimsValuesAndKeepsDescriptionOptional()
    {
        var userId = await fixture.NewUserAsync();

        var withDescription = await fixture.Categories.CreateAsync(userId, new CategoryInput
        {
            Name = "  Coffee ",
            Description = "  Daily espresso  ",
            Color = "#AABBCC",
            MonthlyBudget = 40m
        });
        var withoutDescription = await fixture.Categories.CreateAsync(userId, new CategoryInput
        {
            Name = "Books",
            Description = "   "
        });

        Assert.Equal("Coffee", withDescription.Name);
        Assert.Equal("Daily espresso", withDescription.Description);
        Assert.Equal("#aabbcc", withDescription.Color);
        Assert.Equal(40m, withDescription.MonthlyBudget);
        Assert.Null(withoutDescription.Description);
    }

    [Fact]
    public async Task Create_RejectsDuplicateNameIgnoringCase()
    {
        var userId = await fixture.NewUserAsync();

        // "Food" is a seeded expense category.
        await Assert.ThrowsAsync<ServiceValidationException>(
            () => fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "food" }));
    }

    [Fact]
    public async Task Create_AllowsSameNameForOtherTypeAndOtherUser()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();

        await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "Food", Type = TransactionType.Income });
        await fixture.Categories.CreateAsync(otherUserId, new CategoryInput { Name = "Hobbies" });
        await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "Hobbies" });
    }

    [Fact]
    public async Task Create_RejectsBudgetOnIncomeCategory()
    {
        var userId = await fixture.NewUserAsync();

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Categories.CreateAsync(userId,
            new CategoryInput { Name = "Bonus", Type = TransactionType.Income, MonthlyBudget = 100m }));
    }

    [Fact]
    public async Task Create_RejectsTooLongDescription()
    {
        var userId = await fixture.NewUserAsync();

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Categories.CreateAsync(userId,
            new CategoryInput { Name = "Long", Description = new string('x', 201) }));
    }

    [Fact]
    public async Task Update_ChangesFieldsButRefusesTypeChangeWhileInUse()
    {
        var userId = await fixture.NewUserAsync();
        var category = await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "Gym" });

        var updated = await fixture.Categories.UpdateAsync(userId, category.Id,
            new CategoryInput { Name = "Gym & sport", Description = "Membership", MonthlyBudget = 30m });
        Assert.Equal("Gym & sport", updated.Name);
        Assert.Equal("Membership", updated.Description);

        await fixture.Transactions.CreateAsync(userId, new TransactionInput { Amount = 30m, CategoryId = category.Id });

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Categories.UpdateAsync(userId, category.Id,
            new CategoryInput { Name = "Gym & sport", Type = TransactionType.Income }));
    }

    [Fact]
    public async Task Delete_RefusesCategoryInUse_AndDeletesUnusedOne()
    {
        var userId = await fixture.NewUserAsync();
        var used = await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "Used" });
        var unused = await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "Unused" });
        await fixture.Transactions.CreateAsync(userId, new TransactionInput { Amount = 5m, CategoryId = used.Id });

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Categories.DeleteAsync(userId, used.Id));
        await fixture.Categories.DeleteAsync(userId, unused.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Categories.GetByIdAsync(userId, unused.Id));
    }

    [Fact]
    public async Task GetById_HidesOtherUsersCategories()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        var category = await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "Private" });

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Categories.GetByIdAsync(otherUserId, category.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Categories.GetByIdAsync(userId, "not-an-id"));
    }

    [Fact]
    public async Task GetAll_ReturnsOwnCategoriesSortedByName_AndFiltersByType()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "bills" });
        await fixture.Categories.CreateAsync(otherUserId, new CategoryInput { Name = "Not mine" });

        var all = await fixture.Categories.GetAllAsync(userId);
        var income = await fixture.Categories.GetAllAsync(userId, TransactionType.Income);
        var expenses = await fixture.Categories.GetAllAsync(userId, TransactionType.Expense);

        Assert.Equal(["bills", "Food", "Rent", "Salary"], all.Select(c => c.Name));
        Assert.Equal("Salary", Assert.Single(income).Name);
        Assert.Equal(["bills", "Food", "Rent"], expenses.Select(c => c.Name));
    }

    [Fact]
    public async Task GetById_ReturnsCategory()
    {
        var userId = await fixture.NewUserAsync();
        var created = await fixture.Categories.CreateAsync(userId, new CategoryInput
        {
            Name = "Travel",
            Description = "Trips",
            Color = "#123456",
            MonthlyBudget = 250m
        });

        var loaded = await fixture.Categories.GetByIdAsync(userId, created.Id);

        Assert.Equal(created, loaded);
        Assert.Equal(TransactionType.Expense, loaded.Type);
    }

    [Theory]
    [InlineData("", null, null)]
    [InlineData("   ", null, null)]
    [InlineData("This name is far too long to be accepted by the rules", null, null)]
    [InlineData("Bad color", "red", null)]
    [InlineData("Bad color", "#12345", null)]
    [InlineData("Negative budget", null, -1.0)]
    public async Task Create_RejectsInvalidInput(string name, string? color, double? budget)
    {
        var userId = await fixture.NewUserAsync();

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Categories.CreateAsync(userId,
            new CategoryInput { Name = name, Color = color, MonthlyBudget = (decimal?)budget }));
    }

    [Fact]
    public async Task Update_RejectsNameOfAnotherCategory_ButAllowsRecasingOwnName()
    {
        var userId = await fixture.NewUserAsync();
        var category = await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "Gym" });

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Categories.UpdateAsync(userId, category.Id,
            new CategoryInput { Name = " RENT " }));

        var renamed = await fixture.Categories.UpdateAsync(userId, category.Id, new CategoryInput { Name = "GYM" });
        Assert.Equal("GYM", renamed.Name);
        Assert.Equal("GYM", (await fixture.Categories.GetByIdAsync(userId, category.Id)).Name);
    }

    [Fact]
    public async Task Update_ChangesTypeOfUnusedCategory()
    {
        var userId = await fixture.NewUserAsync();
        var category = await fixture.Categories.CreateAsync(userId,
            new CategoryInput { Name = "Side job", MonthlyBudget = 10m });

        var updated = await fixture.Categories.UpdateAsync(userId, category.Id,
            new CategoryInput { Name = "Side job", Type = TransactionType.Income });

        Assert.Equal(TransactionType.Income, updated.Type);
        Assert.Null(updated.MonthlyBudget);
        Assert.Contains(await fixture.Categories.GetAllAsync(userId, TransactionType.Income), c => c.Id == category.Id);
    }

    [Fact]
    public async Task UpdateAndDelete_OnlyWorkForOwner()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        var category = await fixture.Categories.CreateAsync(userId, new CategoryInput { Name = "Mine" });

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Categories.UpdateAsync(otherUserId, category.Id,
            new CategoryInput { Name = "Stolen" }));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Categories.DeleteAsync(otherUserId, category.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Categories.DeleteAsync(userId, "not-an-id"));

        Assert.Equal("Mine", (await fixture.Categories.GetByIdAsync(userId, category.Id)).Name);
    }

    [Fact]
    public async Task SeedDefaults_TrimsValuesAndSkipsBlankAndDuplicateNames()
    {
        var userId = ObjectId.GenerateNewId().ToString();
        var categories = fixture.CreateCategoryService(
        [
            new() { Name = " Travel ", Type = TransactionType.Expense, Description = "  ", Color = "#00ff00" },
            new() { Name = "TRAVEL", Type = TransactionType.Expense },
            new() { Name = "Travel", Type = TransactionType.Income },
            new() { Name = "   ", Type = TransactionType.Expense }
        ]);

        await categories.SeedDefaultsAsync(userId);

        var seeded = await categories.GetAllAsync(userId);
        Assert.Equal(2, seeded.Count);
        Assert.All(seeded, c => Assert.Equal("Travel", c.Name));
        var expense = Assert.Single(seeded, c => c.Type == TransactionType.Expense);
        Assert.Null(expense.Description);
        Assert.Equal("#00ff00", expense.Color);
    }

    [Fact]
    public async Task SeedDefaults_DoesNothingWithoutConfiguredDefaults()
    {
        var userId = ObjectId.GenerateNewId().ToString();
        var categories = fixture.CreateCategoryService([]);

        await categories.SeedDefaultsAsync(userId);

        Assert.Empty(await categories.GetAllAsync(userId));
    }
}
