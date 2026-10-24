using System.Globalization;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Models;
using MAiMoney.Services.Options;
using Microsoft.Extensions.Options;

namespace MAiMoney.Services.Services.Export;

/// <summary>Loads the month's transactions and totals; the subclasses only write the file.</summary>
public abstract class TransactionExporter(ITransactionService transactions, IOptions<MAiMoneyOptions> options)
    : ITransactionExporter
{
    protected abstract string ContentType { get; }

    protected abstract string Extension { get; }

    public async Task<ExportFile> ExportAsync(
        string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var rows = await transactions.GetAsync(
            userId, new TransactionFilter { Year = year, Month = month }, cancellationToken);

        // GetAsync returns the newest first; a statement reads better in date order.
        var chronological = rows.Reverse().ToList();
        var export = new TransactionExport(
            new DateOnly(year, month, 1),
            chronological,
            TotalIncome: chronological.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
            TotalExpenses: chronological.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
            options.Value.CurrencyCode);

        return new ExportFile(Write(export), ContentType, $"maimoney-transactions-{year:D4}-{month:D2}.{Extension}");
    }

    protected abstract byte[] Write(TransactionExport export);

    protected static string[] Headers(TransactionExport export) =>
        ["Date", "Type", "Category", "Description", $"Amount ({export.Currency})"];

    protected static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}

public record TransactionExport(
    DateOnly Month,
    IReadOnlyList<TransactionDto> Rows,
    decimal TotalIncome,
    decimal TotalExpenses,
    string Currency)
{
    public decimal Balance => TotalIncome - TotalExpenses;

    public string MonthName => Month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
}
