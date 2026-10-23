using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using MAiMoney.Services.Models;

namespace MAiMoney.Services.Tests;

[Collection(MongoCollection.Name)]
public class TransactionServiceTests(MongoFixture fixture)
{
    [Fact]
    public async Task Create_ReturnsTransactionWithCategoryDetails()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");

        var created = await fixture.Transactions.CreateAsync(userId, new TransactionInput
        {
            Amount = 12.5m,
            Date = new DateOnly(2026, 3, 15),
            CategoryId = food.Id,
            Description = "  Lunch "
        });
        var loaded = await fixture.Transactions.GetByIdAsync(userId, created.Id);

        Assert.Equal(12.5m, loaded.Amount);
        Assert.Equal(new DateOnly(2026, 3, 15), loaded.Date);
        Assert.Equal("Food", loaded.CategoryName);
        Assert.Equal("Lunch", loaded.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(1.234)]
    public async Task Create_RejectsInvalidAmount(double? amount)
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Transactions.CreateAsync(userId,
            new TransactionInput { Amount = (decimal?)amount, CategoryId = food.Id }));
    }

    [Fact]
    public async Task Create_RejectsOtherUsersCategory()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        var othersFood = await CategoryAsync(otherUserId, "Food");

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Transactions.CreateAsync(userId,
            new TransactionInput { Amount = 10m, CategoryId = othersFood.Id }));
    }

    [Fact]
    public async Task Create_RejectsCategoryOfOtherType()
    {
        var userId = await fixture.NewUserAsync();
        var salary = await CategoryAsync(userId, "Salary");

        var ex = await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Transactions.CreateAsync(userId,
            new TransactionInput { Type = TransactionType.Expense, Amount = 10m, CategoryId = salary.Id }));
        Assert.Contains("income category", ex.Message);
    }

    [Fact]
    public async Task Get_FiltersByMonthTypeAndCategory_NewestFirst()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");
        var rent = await CategoryAsync(userId, "Rent");
        var salary = await CategoryAsync(userId, "Salary");

        await AddAsync(userId, food, 10m, new DateOnly(2026, 4, 1));
        await AddAsync(userId, food, 20m, new DateOnly(2026, 4, 30));
        await AddAsync(userId, rent, 500m, new DateOnly(2026, 4, 2));
        await AddAsync(userId, salary, 2000m, new DateOnly(2026, 4, 25), TransactionType.Income);
        await AddAsync(userId, food, 99m, new DateOnly(2026, 5, 1));

        var april = await fixture.Transactions.GetAsync(userId, new TransactionFilter { Year = 2026, Month = 4 });
        var aprilExpenses = await fixture.Transactions.GetAsync(userId,
            new TransactionFilter { Year = 2026, Month = 4, Type = TransactionType.Expense });
        var aprilFood = await fixture.Transactions.GetAsync(userId,
            new TransactionFilter { Year = 2026, Month = 4, CategoryId = food.Id, Limit = 1 });

        Assert.Equal([20m, 2000m, 500m, 10m], april.Select(t => t.Amount));
        Assert.Equal(3, aprilExpenses.Count);
        Assert.Equal(20m, Assert.Single(aprilFood).Amount);
    }

    [Fact]
    public async Task MonthlySummary_TotalsIncomeExpensesAndBudgets()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");
        var rent = await CategoryAsync(userId, "Rent");
        var salary = await CategoryAsync(userId, "Salary");
        await fixture.Categories.UpdateAsync(userId, food.Id, new CategoryInput { Name = "Food", MonthlyBudget = 100m });
        await fixture.Categories.UpdateAsync(userId, rent.Id, new CategoryInput { Name = "Rent", MonthlyBudget = 800m });

        await AddAsync(userId, food, 60.25m, new DateOnly(2026, 6, 3));
        await AddAsync(userId, food, 50m, new DateOnly(2026, 6, 20));
        await AddAsync(userId, salary, 2500m, new DateOnly(2026, 6, 1), TransactionType.Income);
        await AddAsync(userId, food, 999m, new DateOnly(2026, 7, 1));

        var summary = await fixture.Transactions.GetMonthlySummaryAsync(userId, 2026, 6);

        Assert.Equal(2500m, summary.TotalIncome);
        Assert.Equal(110.25m, summary.TotalExpenses);
        Assert.Equal(2389.75m, summary.Balance);

        var foodSummary = summary.Categories.Single(c => c.CategoryId == food.Id);
        Assert.Equal(110.25m, foodSummary.Total);
        Assert.True(foodSummary.IsOverBudget);
        Assert.Equal(110.3m, foodSummary.BudgetUsedPercent);

        // Rent has a budget but no spending yet, so it's listed with zero.
        var rentSummary = summary.Categories.Single(c => c.CategoryId == rent.Id);
        Assert.Equal(0m, rentSummary.Total);
        Assert.False(rentSummary.IsOverBudget);
    }

    [Fact]
    public async Task UpdateAndDelete_OnlyWorkForOwner()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");
        var transaction = await AddAsync(userId, food, 15m, new DateOnly(2026, 2, 2));

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Transactions.DeleteAsync(otherUserId, transaction.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Transactions.UpdateAsync(otherUserId, transaction.Id,
            new TransactionInput { Amount = 1m, CategoryId = food.Id }));

        var updated = await fixture.Transactions.UpdateAsync(userId, transaction.Id,
            new TransactionInput { Amount = 16m, Date = new DateOnly(2026, 2, 3), CategoryId = food.Id });
        Assert.Equal(16m, updated.Amount);

        await fixture.Transactions.DeleteAsync(userId, transaction.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Transactions.GetByIdAsync(userId, transaction.Id));
    }

    private async Task<CategoryDto> CategoryAsync(string userId, string name) =>
        (await fixture.Categories.GetAllAsync(userId)).Single(c => c.Name == name);

    private Task<TransactionDto> AddAsync(
        string userId, CategoryDto category, decimal amount, DateOnly date, TransactionType type = TransactionType.Expense) =>
        fixture.Transactions.CreateAsync(userId, new TransactionInput
        {
            Type = type,
            Amount = amount,
            Date = date,
            CategoryId = category.Id
        });
}
