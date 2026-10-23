using System.ComponentModel.DataAnnotations;
using MAiMoney.Services.Entities;

namespace MAiMoney.Services.Options;

public class MAiMoneyOptions
{
    public const string SectionName = "MAiMoney";

    /// <summary>ISO 4217 code shown next to amounts, e.g. "EUR".</summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Categories created for every newly registered user.</summary>
    public List<DefaultCategoryOptions> DefaultCategories { get; set; } = [];
}

public class DefaultCategoryOptions
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TransactionType Type { get; set; }

    public string? Color { get; set; }
}
