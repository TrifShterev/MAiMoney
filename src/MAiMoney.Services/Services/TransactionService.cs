using MAiMoney.Services.Data;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using MAiMoney.Services.Models;
using MongoDB.Driver;

namespace MAiMoney.Services.Services;

public class TransactionService(MongoDbContext db) : ITransactionService
{
    public async Task<IReadOnlyList<TransactionDto>> GetAsync(
        string userId, TransactionFilter filter, CancellationToken cancellationToken = default)
    {
        var builder = Builders<Transaction>.Filter;
        var query = builder.Eq(t => t.UserId, userId);

        if (filter.Year is not null || filter.Month is not null)
        {
            if (filter.Year is null || filter.Month is null)
            {
                throw new ServiceValidationException("Year and month must be given together.");
            }

            var (from, to) = MonthRange(filter.Year.Value, filter.Month.Value);
            query &= builder.Gte(t => t.Date, from) & builder.Lt(t => t.Date, to);
        }

        if (filter.Type is { } type)
        {
            query &= builder.Eq(t => t.Type, type);
        }

        if (!string.IsNullOrEmpty(filter.CategoryId))
        {
            if (!InputValidator.IsValidId(filter.CategoryId))
            {
                return [];
            }

            query &= builder.Eq(t => t.CategoryId, filter.CategoryId);
        }

        IFindFluent<Transaction, Transaction> find = db.Transactions.Find(query)
            .SortByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt);
        if (filter.Limit is > 0)
        {
            find = find.Limit(filter.Limit);
        }

        var transactions = await find.ToListAsync(cancellationToken);
        var categories = await LoadCategoriesAsync(userId, cancellationToken);
        return transactions.Select(t => ToDto(t, categories.GetValueOrDefault(t.CategoryId))).ToList();
    }

    public async Task<TransactionDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var transaction = await GetOwnedAsync(userId, id, cancellationToken);
        var category = await db.Categories
            .Find(c => c.Id == transaction.CategoryId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
        return ToDto(transaction, category);
    }

    public async Task<TransactionDto> CreateAsync(
        string userId, TransactionInput input, CancellationToken cancellationToken = default)
    {
        var category = await ValidateAsync(userId, input, cancellationToken);
        var now = DateTime.UtcNow;
        var transaction = new Transaction
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(input, category, transaction);

        await db.Transactions.InsertOneAsync(transaction, cancellationToken: cancellationToken);
        return ToDto(transaction, category);
    }

    public async Task<TransactionDto> UpdateAsync(
        string userId, string id, TransactionInput input, CancellationToken cancellationToken = default)
    {
        var transaction = await GetOwnedAsync(userId, id, cancellationToken);
        var category = await ValidateAsync(userId, input, cancellationToken);
        Apply(input, category, transaction);
        transaction.UpdatedAt = DateTime.UtcNow;

        await db.Transactions.ReplaceOneAsync(
            t => t.Id == id && t.UserId == userId, transaction, cancellationToken: cancellationToken);
        return ToDto(transaction, category);
    }

    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var result = InputValidator.IsValidId(id)
            ? await db.Transactions.DeleteOneAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
            : null;
        if (result is not { DeletedCount: > 0 })
        {
            throw new NotFoundException("Transaction not found.");
        }
    }

    public async Task<MonthlySummary> GetMonthlySummaryAsync(
        string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var (from, to) = MonthRange(year, month);

        var totals = await db.Transactions.Aggregate()
            .Match(t => t.UserId == userId && t.Date >= from && t.Date < to)
            .Group(
                t => new { t.CategoryId, t.Type },
                g => new { g.Key.CategoryId, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var categories = await LoadCategoriesAsync(userId, cancellationToken);
        var totalsByCategory = totals.ToDictionary(t => (t.CategoryId, t.Type), t => t.Total);

        // Every category that had activity this month, plus expense categories with a budget
        // so the user can see how much of it is still left.
        var summaries = categories.Values
            .Select(c => new CategorySummary(
                c.Id, c.Name, c.Color, c.Type, totalsByCategory.GetValueOrDefault((c.Id, c.Type)), c.MonthlyBudget))
            .Where(s => s.Total > 0 || s.MonthlyBudget is not null)
            .OrderBy(s => s.Type == TransactionType.Expense ? 0 : 1)
            .ThenByDescending(s => s.Total)
            .ThenBy(s => s.Name)
            .ToList();

        return new MonthlySummary(
            year,
            month,
            TotalIncome: totals.Where(t => t.Type == TransactionType.Income).Sum(t => t.Total),
            TotalExpenses: totals.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Total),
            summaries);
    }

    public async Task<IReadOnlyList<DailyTotal>> GetDailyTotalsAsync(
        string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var (from, to) = MonthRange(year, month);

        // Dates are stored as midnight UTC, so grouping by the date groups by day.
        var totals = await db.Transactions.Aggregate()
            .Match(t => t.UserId == userId && t.Date >= from && t.Date < to)
            .Group(
                t => new { t.Date, t.Type },
                g => new { g.Key.Date, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var totalsByDay = totals.ToDictionary(t => (DateOnly.FromDateTime(t.Date), t.Type), t => t.Total);
        return Enumerable.Range(1, DateTime.DaysInMonth(year, month))
            .Select(day => new DateOnly(year, month, day))
            .Select(date => new DailyTotal(
                date,
                totalsByDay.GetValueOrDefault((date, TransactionType.Income)),
                totalsByDay.GetValueOrDefault((date, TransactionType.Expense))))
            .ToList();
    }

    private async Task<Transaction> GetOwnedAsync(string userId, string id, CancellationToken cancellationToken)
    {
        var transaction = InputValidator.IsValidId(id)
            ? await db.Transactions.Find(t => t.Id == id && t.UserId == userId).FirstOrDefaultAsync(cancellationToken)
            : null;
        return transaction ?? throw new NotFoundException("Transaction not found.");
    }

    /// <summary>Checks the input and returns the category it points to.</summary>
    private async Task<Category> ValidateAsync(string userId, TransactionInput input, CancellationToken cancellationToken)
    {
        InputValidator.Validate(input);

        if (decimal.Round(input.Amount!.Value, 2) != input.Amount.Value)
        {
            throw new ServiceValidationException("Amount can have at most two decimal places.");
        }

        var category = InputValidator.IsValidId(input.CategoryId)
            ? await db.Categories
                .Find(c => c.Id == input.CategoryId && c.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        if (category is null)
        {
            throw new ServiceValidationException("The selected category does not exist.");
        }

        if (category.Type != input.Type)
        {
            throw new ServiceValidationException(
                $"\"{category.Name}\" is an {category.Type.ToString().ToLowerInvariant()} category " +
                $"and can't be used for an {input.Type.ToString().ToLowerInvariant()}.");
        }

        return category;
    }

    private static void Apply(TransactionInput input, Category category, Transaction transaction)
    {
        transaction.CategoryId = category.Id;
        transaction.Type = input.Type;
        transaction.Amount = input.Amount!.Value;
        transaction.Date = DateTime.SpecifyKind(input.Date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        transaction.Description = InputValidator.Clean(input.Description);
    }

    private async Task<Dictionary<string, Category>> LoadCategoriesAsync(string userId, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.Find(c => c.UserId == userId).ToListAsync(cancellationToken);
        return categories.ToDictionary(c => c.Id);
    }

    private static (DateTime From, DateTime To) MonthRange(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            throw new ServiceValidationException("Please choose a valid month.");
        }

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, from.AddMonths(1));
    }

    private static TransactionDto ToDto(Transaction t, Category? category) =>
        new(
            t.Id,
            t.Type,
            t.Amount,
            DateOnly.FromDateTime(t.Date),
            t.CategoryId,
            category?.Name ?? "(deleted category)",
            category?.Color,
            t.Description);
}
