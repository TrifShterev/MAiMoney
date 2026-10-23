using System.ComponentModel.DataAnnotations;
using MAiMoney.Services.Entities;

namespace MAiMoney.Services.Models;

/// <summary>Data needed to create or update a category.</summary>
public class CategoryInput
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(50, ErrorMessage = "Name can be at most 50 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Description can be at most 200 characters.")]
    public string? Description { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Expense;

    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Color must look like #1a2b3c.")]
    public string? Color { get; set; }

    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Budget must be zero or a positive amount.")]
    public decimal? MonthlyBudget { get; set; }
}
