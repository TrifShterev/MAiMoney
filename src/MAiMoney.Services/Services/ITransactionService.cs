using MAiMoney.Services.Models;

namespace MAiMoney.Services.Services;

public interface ITransactionService
{
    /// <summary>Lists the user's transactions, newest first.</summary>
    Task<IReadOnlyList<TransactionDto>> GetAsync(string userId, TransactionFilter filter, CancellationToken cancellationToken = default);

    Task<TransactionDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default);

    Task<TransactionDto> CreateAsync(string userId, TransactionInput input, CancellationToken cancellationToken = default);

    Task<TransactionDto> UpdateAsync(string userId, string id, TransactionInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>Income and expense totals for one month, broken down by category and compared to budgets.</summary>
    Task<MonthlySummary> GetMonthlySummaryAsync(string userId, int year, int month, CancellationToken cancellationToken = default);
}
