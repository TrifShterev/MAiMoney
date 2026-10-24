using MAiMoney.Services.Models;

namespace MAiMoney.Services.Services.Export;

/// <summary>
/// Writes all transactions of a month to a file. Every format is registered as a keyed service
/// under its <c>Key</c>, e.g. <see cref="CsvTransactionExporter.Key"/>.
/// </summary>
public interface ITransactionExporter
{
    Task<ExportFile> ExportAsync(string userId, int year, int month, CancellationToken cancellationToken = default);
}
