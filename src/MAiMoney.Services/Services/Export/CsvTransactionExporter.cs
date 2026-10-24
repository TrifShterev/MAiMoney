using System.Text;
using MAiMoney.Services.Options;
using Microsoft.Extensions.Options;

namespace MAiMoney.Services.Services.Export;

/// <summary>Plain RFC 4180 CSV with the transaction rows only.</summary>
public class CsvTransactionExporter(ITransactionService transactions, IOptions<MAiMoneyOptions> options)
    : TransactionExporter(transactions, options)
{
    public const string Key = "csv";

    protected override string ContentType => "text/csv; charset=utf-8";

    protected override string Extension => "csv";

    protected override byte[] Write(TransactionExport export)
    {
        var csv = new StringBuilder();
        AppendRow(csv, Headers(export));
        foreach (var t in export.Rows)
        {
            AppendRow(csv, [FormatDate(t.Date), t.Type.ToString(), Text(t.CategoryName), Text(t.Description), FormatAmount(t.Amount)]);
        }

        // With the BOM, Excel reads the file as UTF-8 instead of the system code page.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> cells)
    {
        csv.AppendJoin(',', cells.Select(Quote));
        csv.Append("\r\n");
    }

    private static string Quote(string cell) =>
        cell.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{cell.Replace("\"", "\"\"")}\"" : cell;

    // Spreadsheets run a cell that starts like a formula, so user text gets a leading apostrophe.
    private static string Text(string? value) =>
        value is { Length: > 0 } && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value ?? string.Empty;
}
