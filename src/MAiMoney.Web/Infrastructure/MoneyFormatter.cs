using System.Globalization;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Options;
using Microsoft.Extensions.Options;

namespace MAiMoney.Web.Infrastructure;

/// <summary>Formats amounts with the configured currency, e.g. "1,250.00 EUR".</summary>
public class MoneyFormatter(IOptions<MAiMoneyOptions> options)
{
    public string Currency => options.Value.CurrencyCode;

    public string Format(decimal amount) =>
        $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {Currency}";

    /// <summary>Adds a + for income and a − for expenses.</summary>
    public string FormatSigned(decimal amount, TransactionType type) =>
        (type == TransactionType.Income ? "+" : "−") + Format(amount);
}
