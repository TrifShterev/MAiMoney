namespace MAiMoney.Services.Models;

public record DailyTotal(DateOnly Date, decimal Income, decimal Expenses);
