using System.Globalization;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Options;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MAiMoney.Services.Services.Export;

/// <summary>A printable A4 statement of the month with its totals.</summary>
public class PdfTransactionExporter(ITransactionService transactions, IOptions<MAiMoneyOptions> options)
    : TransactionExporter(transactions, options)
{
    public const string Key = "pdf";

    // The app's own brand, income and expense colors.
    private const string Brand = "#1F7A63";
    private const string Income = "#15803D";
    private const string Expense = "#D1343A";
    private const string Muted = "#667085";
    private const string Line = "#E4E7EC";

    static PdfTransactionExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    protected override string ContentType => "application/pdf";

    protected override string Extension => "pdf";

    protected override byte[] Write(TransactionExport export) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(text => text.FontSize(9.5f));

            page.Header().PaddingBottom(14).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("MAiMoney").FontSize(10).SemiBold().FontColor(Brand);
                    column.Item().Text($"Transactions · {export.MonthName}").FontSize(18).Bold();
                });
                row.AutoItem().AlignBottom().Text($"Exported {FormatDate(DateOnly.FromDateTime(DateTime.Today))}")
                    .FontColor(Muted);
            });

            page.Content().Column(column =>
            {
                if (export.Rows.Count == 0)
                {
                    column.Item().PaddingVertical(20).Text($"No transactions in {export.MonthName}.").FontColor(Muted);
                }
                else
                {
                    column.Item().Element(container => RowsTable(container, export));
                }

                column.Item().PaddingTop(16).AlignRight().Width(220).Element(container => Totals(container, export));
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();

    private static void RowsTable(IContainer container, TransactionExport export) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(62);
                columns.ConstantColumn(52);
                columns.RelativeColumn(2);
                columns.RelativeColumn(3);
                columns.ConstantColumn(90);
            });

            table.Header(header =>
            {
                var headers = Headers(export);
                for (var i = 0; i < headers.Length; i++)
                {
                    var cell = header.Cell().BorderBottom(1).BorderColor(Brand).PaddingVertical(5);
                    (i == headers.Length - 1 ? cell.AlignRight() : cell).Text(headers[i]).SemiBold();
                }
            });

            foreach (var t in export.Rows)
            {
                var isIncome = t.Type == TransactionType.Income;
                Cell(table).Text(FormatDate(t.Date));
                Cell(table).Text(t.Type.ToString()).FontColor(Muted);
                Cell(table).Text(t.CategoryName);
                Cell(table).Text(t.Description ?? string.Empty);
                Cell(table).AlignRight()
                    .Text((isIncome ? "+" : "−") + Money(t.Amount, export.Currency))
                    .FontColor(isIncome ? Income : Expense);
            }
        });

    private static IContainer Cell(TableDescriptor table) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(4).PaddingRight(6);

    private static void Totals(IContainer container, TransactionExport export) =>
        container.Column(column =>
        {
            TotalRow(column, "Total income", Money(export.TotalIncome, export.Currency), Income);
            TotalRow(column, "Total expenses", Money(export.TotalExpenses, export.Currency), Expense);
            column.Item().PaddingTop(4).BorderTop(1).BorderColor(Brand).PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Balance").Bold();
                row.AutoItem().Text(Money(export.Balance, export.Currency)).Bold()
                    .FontColor(export.Balance < 0 ? Expense : Colors.Black);
            });
        });

    private static void TotalRow(ColumnDescriptor column, string label, string value, string color) =>
        column.Item().PaddingVertical(2).Row(row =>
        {
            row.RelativeItem().Text(label);
            row.AutoItem().Text(value).SemiBold().FontColor(color);
        });

    // Same look as MoneyFormatter in the web app, e.g. "1,250.00 EUR".
    private static string Money(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";
}
