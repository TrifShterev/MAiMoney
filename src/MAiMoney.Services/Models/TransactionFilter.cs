using MAiMoney.Services.Entities;

namespace MAiMoney.Services.Models;

/// <summary>Optional filters for listing transactions. Year and Month must be given together.</summary>
public class TransactionFilter
{
    public int? Year { get; set; }

    public int? Month { get; set; }

    public TransactionType? Type { get; set; }

    public string? CategoryId { get; set; }

    /// <summary>Maximum number of results, newest first.</summary>
    public int? Limit { get; set; }
}
