using ClosedXML.Excel;
using MAiMoney.Services.Options;
using Microsoft.Extensions.Options;

namespace MAiMoney.Services.Services.Export;

/// <summary>An .xlsx workbook with real dates and numbers and the month's totals below the rows.</summary>
public class ExcelTransactionExporter(ITransactionService transactions, IOptions<MAiMoneyOptions> options)
    : TransactionExporter(transactions, options)
{
    public const string Key = "excel";

    private const string AmountFormat = "#,##0.00";

    protected override string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    protected override string Extension => "xlsx";

    protected override byte[] Write(TransactionExport export)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Transactions");

        var headers = Headers(export);
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F2EF");
        sheet.SheetView.FreezeRows(1);

        var row = 2;
        foreach (var t in export.Rows)
        {
            sheet.Cell(row, 1).Value = t.Date.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 2).Value = t.Type.ToString();
            sheet.Cell(row, 3).Value = t.CategoryName;
            sheet.Cell(row, 4).Value = t.Description ?? string.Empty;
            sheet.Cell(row, 5).Value = t.Amount;
            row++;
        }

        sheet.Range(1, 1, Math.Max(row - 1, 1), headers.Length).SetAutoFilter();
        sheet.Column(1).Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Column(5).Style.NumberFormat.Format = AmountFormat;

        row++;
        foreach (var (label, value) in new[]
                 {
                     ("Total income", export.TotalIncome),
                     ("Total expenses", export.TotalExpenses),
                     ("Balance", export.Balance)
                 })
        {
            sheet.Cell(row, 4).Value = label;
            sheet.Cell(row, 4).Style.Font.Bold = true;
            sheet.Cell(row, 5).Value = value;
            sheet.Cell(row, 5).Style.Font.Bold = true;
            row++;
        }

        sheet.Columns(1, headers.Length).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
