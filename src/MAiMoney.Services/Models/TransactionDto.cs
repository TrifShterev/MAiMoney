using MAiMoney.Services.Entities;

namespace MAiMoney.Services.Models;

public record TransactionDto(
    string Id,
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string CategoryId,
    string CategoryName,
    string? CategoryColor,
    string? Description);
