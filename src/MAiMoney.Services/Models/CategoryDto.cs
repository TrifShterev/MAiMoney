using MAiMoney.Services.Entities;

namespace MAiMoney.Services.Models;

public record CategoryDto(
    string Id,
    string Name,
    string? Description,
    TransactionType Type,
    string? Color,
    decimal? MonthlyBudget)
{
    public static CategoryDto From(Category c) =>
        new(c.Id, c.Name, c.Description, c.Type, c.Color, c.MonthlyBudget);
}
