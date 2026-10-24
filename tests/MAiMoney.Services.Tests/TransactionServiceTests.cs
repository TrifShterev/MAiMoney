using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using MAiMoney.Services.Models;
using MongoDB.Bson;
using MongoDB.Driver;

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

    [Fact]
    public async Task Create_StoresBlankDescriptionAsNull()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");

        var created = await fixture.Transactions.CreateAsync(userId,
            new TransactionInput { Amount = 3m, CategoryId = food.Id, Description = "   " });

        Assert.Null(created.Description);
        Assert.Equal(TransactionType.Expense, created.Type);
        Assert.Equal(food.Id, created.CategoryId);
    }

    [Fact]
    public async Task Create_RejectsTooLongDescription()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Transactions.CreateAsync(userId,
            new TransactionInput { Amount = 3m, CategoryId = food.Id, Description = new string('x', 201) }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-id")]
    [InlineData("0123456789abcdef01234567")]
    public async Task Create_RejectsMissingOrUnknownCategory(string? categoryId)
    {
        var userId = await fixture.NewUserAsync();

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Transactions.CreateAsync(userId,
            new TransactionInput { Amount = 10m, CategoryId = categoryId }));
    }

    [Fact]
    public async Task GetById_HidesOtherUsersTransactions()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        var transaction = await AddAsync(userId, await CategoryAsync(userId, "Food"), 8m, new DateOnly(2026, 1, 5));

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Transactions.GetByIdAsync(otherUserId, transaction.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Transactions.GetByIdAsync(userId, "not-an-id"));
    }

    [Fact]
    public async Task Get_OnlyReturnsOwnTransactions()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        await AddAsync(userId, await CategoryAsync(userId, "Food"), 1m, new DateOnly(2026, 1, 1));
        await AddAsync(otherUserId, await CategoryAsync(otherUserId, "Food"), 2m, new DateOnly(2026, 1, 1));

        var transactions = await fixture.Transactions.GetAsync(userId, new TransactionFilter());

        Assert.Equal(1m, Assert.Single(transactions).Amount);
    }

    [Theory]
    [InlineData(2026, null)]
    [InlineData(null, 4)]
    [InlineData(2026, 13)]
    [InlineData(2026, 0)]
    [InlineData(1999, 12)]
    public async Task Get_RejectsInvalidMonth(int? year, int? month)
    {
        var userId = await fixture.NewUserAsync();

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Transactions.GetAsync(userId,
            new TransactionFilter { Year = year, Month = month }));
    }

    [Fact]
    public async Task Get_ReturnsNothingForInvalidCategoryId()
    {
        var userId = await fixture.NewUserAsync();
        await AddAsync(userId, await CategoryAsync(userId, "Food"), 4m, new DateOnly(2026, 1, 1));

        Assert.Empty(await fixture.Transactions.GetAsync(userId, new TransactionFilter { CategoryId = "not-an-id" }));
    }

    [Fact]
    public async Task Get_ShowsPlaceholderNameWhenCategoryIsGone()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");
        await AddAsync(userId, food, 4m, new DateOnly(2026, 1, 1));

        // The service refuses to delete a category in use, so remove it behind its back.
        await fixture.Db.Categories.DeleteOneAsync(c => c.Id == food.Id);

        var transaction = Assert.Single(await fixture.Transactions.GetAsync(userId, new TransactionFilter()));
        Assert.Equal("(deleted category)", transaction.CategoryName);
        Assert.Null(transaction.CategoryColor);
    }

    [Fact]
    public async Task Update_CanSwitchToCategoryOfOtherType()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");
        var salary = await CategoryAsync(userId, "Salary");
        var transaction = await AddAsync(userId, food, 15m, new DateOnly(2026, 2, 2));

        await fixture.Transactions.UpdateAsync(userId, transaction.Id, new TransactionInput
        {
            Type = TransactionType.Income,
            Amount = 15m,
            Date = new DateOnly(2026, 2, 2),
            CategoryId = salary.Id,
            Description = "Refund"
        });
        var loaded = await fixture.Transactions.GetByIdAsync(userId, transaction.Id);

        Assert.Equal(TransactionType.Income, loaded.Type);
        Assert.Equal("Salary", loaded.CategoryName);
        Assert.Equal("Refund", loaded.Description);
    }

    [Fact]
    public async Task Update_RejectsInvalidInputAndKeepsTransaction()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");
        var salary = await CategoryAsync(userId, "Salary");
        var transaction = await AddAsync(userId, food, 15m, new DateOnly(2026, 2, 2));

        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Transactions.UpdateAsync(userId,
            transaction.Id, new TransactionInput { Amount = 15m, CategoryId = salary.Id }));
        await Assert.ThrowsAsync<ServiceValidationException>(() => fixture.Transactions.UpdateAsync(userId,
            transaction.Id, new TransactionInput { Amount = 0m, CategoryId = food.Id }));

        Assert.Equal(transaction, await fixture.Transactions.GetByIdAsync(userId, transaction.Id));
    }

    [Fact]
    public async Task Delete_RejectsUnknownId()
    {
        var userId = await fixture.NewUserAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Transactions.DeleteAsync(userId, "not-an-id"));
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Transactions.DeleteAsync(userId, ObjectId.GenerateNewId().ToString()));
    }

    [Fact]
    public async Task MonthlySummary_IsEmptyForMonthWithoutActivity()
    {
        var userId = await fixture.NewUserAsync();

        var summary = await fixture.Transactions.GetMonthlySummaryAsync(userId, 2026, 8);

        Assert.Equal((2026, 8), (summary.Year, summary.Month));
        Assert.Equal(0m, summary.TotalIncome);
        Assert.Equal(0m, summary.TotalExpenses);
        Assert.Equal(0m, summary.Balance);
        Assert.Empty(summary.Categories);
    }

    [Fact]
    public async Task MonthlySummary_ListsExpensesFirstByTotal_AndIgnoresOtherUsers()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");
        var rent = await CategoryAsync(userId, "Rent");
        var salary = await CategoryAsync(userId, "Salary");

        await AddAsync(userId, salary, 3000m, new DateOnly(2026, 9, 1), TransactionType.Income);
        await AddAsync(userId, food, 40m, new DateOnly(2026, 9, 2));
        await AddAsync(userId, rent, 700m, new DateOnly(2026, 9, 3));
        await AddAsync(otherUserId, await CategoryAsync(otherUserId, "Food"), 5000m, new DateOnly(2026, 9, 4));

        var summary = await fixture.Transactions.GetMonthlySummaryAsync(userId, 2026, 9);

        Assert.Equal(3000m, summary.TotalIncome);
        Assert.Equal(740m, summary.TotalExpenses);
        Assert.Equal(["Rent", "Food", "Salary"], summary.Categories.Select(c => c.Name));
        Assert.All(summary.Categories, c => Assert.Null(c.BudgetUsedPercent));
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(2101, 1)]
    public async Task MonthlySummary_RejectsInvalidMonth(int year, int month)
    {
        var userId = await fixture.NewUserAsync();

        await Assert.ThrowsAsync<ServiceValidationException>(
            () => fixture.Transactions.GetMonthlySummaryAsync(userId, year, month));
    }

    [Fact]
    public async Task DailyTotals_ReturnsEveryDayOfMonth_WithSumsPerType()
    {
        var userId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");
        var rent = await CategoryAsync(userId, "Rent");
        var salary = await CategoryAsync(userId, "Salary");

        await AddAsync(userId, food, 12.5m, new DateOnly(2026, 6, 3));
        await AddAsync(userId, rent, 700m, new DateOnly(2026, 6, 3));
        await AddAsync(userId, salary, 3000m, new DateOnly(2026, 6, 30), TransactionType.Income);

        var days = await fixture.Transactions.GetDailyTotalsAsync(userId, 2026, 6);

        Assert.Equal(30, days.Count);
        Assert.Equal(Enumerable.Range(1, 30).Select(d => new DateOnly(2026, 6, d)), days.Select(d => d.Date));
        Assert.Equal(new DailyTotal(new DateOnly(2026, 6, 3), 0m, 712.5m), days[2]);
        Assert.Equal(new DailyTotal(new DateOnly(2026, 6, 30), 3000m, 0m), days[29]);
        Assert.All(days.Where(d => d.Date.Day is not (3 or 30)), d => Assert.Equal((0m, 0m), (d.Income, d.Expenses)));
    }

    [Fact]
    public async Task DailyTotals_IgnoresOtherUsersAndOtherMonths()
    {
        var userId = await fixture.NewUserAsync();
        var otherUserId = await fixture.NewUserAsync();
        var food = await CategoryAsync(userId, "Food");

        await AddAsync(userId, food, 20m, new DateOnly(2026, 2, 1));
        await AddAsync(userId, food, 30m, new DateOnly(2026, 1, 31));
        await AddAsync(userId, food, 40m, new DateOnly(2026, 3, 1));
        await AddAsync(otherUserId, await CategoryAsync(otherUserId, "Food"), 500m, new DateOnly(2026, 2, 1));

        var days = await fixture.Transactions.GetDailyTotalsAsync(userId, 2026, 2);

        Assert.Equal(28, days.Count);
        Assert.Equal(20m, days[0].Expenses);
        Assert.Equal(20m, days.Sum(d => d.Expenses));
        Assert.Equal(0m, days.Sum(d => d.Income));
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 12)]
    public async Task DailyTotals_RejectsInvalidMonth(int year, int month)
    {
        var userId = await fixture.NewUserAsync();

        await Assert.ThrowsAsync<ServiceValidationException>(
            () => fixture.Transactions.GetDailyTotalsAsync(userId, year, month));
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
