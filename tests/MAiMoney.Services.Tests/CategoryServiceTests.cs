using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using MAiMoney.Services.Models;

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
}
