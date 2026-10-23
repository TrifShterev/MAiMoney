using MAiMoney.Services.Entities;

namespace MAiMoney.Services.Models;

public record MonthlySummary(
    int Year,
    int Month,
    decimal TotalIncome,
    decimal TotalExpenses,
    IReadOnlyList<CategorySummary> Categories)
{
    public decimal Balance => TotalIncome - TotalExpenses;
}

public record CategorySummary(
    string CategoryId,
    string Name,
    string? Color,
    TransactionType Type,
    decimal Total,
    decimal? MonthlyBudget)
{
    /// <summary>Share of the budget used, in percent; null when the category has no budget.</summary>
    public decimal? BudgetUsedPercent =>
        MonthlyBudget is > 0 ? Math.Round(Total / MonthlyBudget.Value * 100, 1, MidpointRounding.AwayFromZero) : null;

    public bool IsOverBudget => MonthlyBudget.HasValue && Total > MonthlyBudget.Value;
}
