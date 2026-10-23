using System.ComponentModel.DataAnnotations;
using MAiMoney.Services.Entities;

namespace MAiMoney.Services.Models;

/// <summary>Data needed to create or update a transaction.</summary>
public class TransactionInput
{
    public TransactionType Type { get; set; } = TransactionType.Expense;

    [Required(ErrorMessage = "Please enter an amount.")]
    [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = "Amount must be greater than zero.")]
    public decimal? Amount { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Please select a category.")]
    public string? CategoryId { get; set; }

    [StringLength(200, ErrorMessage = "Description can be at most 200 characters.")]
    public string? Description { get; set; }
}
