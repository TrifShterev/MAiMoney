using MAiMoney.Services.Models;
using MAiMoney.Services.Services;
using MAiMoney.Services.Services.Export;
using MAiMoney.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAiMoney.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public class TransactionsController(ITransactionService transactions) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TransactionDto>> GetAll([FromQuery] TransactionFilter filter, CancellationToken cancellationToken) =>
        transactions.GetAsync(User.GetUserId(), filter, cancellationToken);

    [HttpGet("summary")]
    public Task<MonthlySummary> GetSummary([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken) =>
        transactions.GetMonthlySummaryAsync(User.GetUserId(), year, month, cancellationToken);

    [HttpGet("daily")]
    public Task<IReadOnlyList<DailyTotal>> GetDaily([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken) =>
        transactions.GetDailyTotalsAsync(User.GetUserId(), year, month, cancellationToken);

    /// <summary>Downloads every transaction of the month; <paramref name="format"/> is an exporter key (csv, excel, pdf).</summary>
    [HttpGet("export/{format}")]
    public async Task<IActionResult> Export(
        string format, [FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        // The key comes from the URL, so the exporter can't be injected with [FromKeyedServices].
        var exporter = HttpContext.RequestServices.GetKeyedService<ITransactionExporter>(format.ToLowerInvariant());
        if (exporter is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Unknown export format.");
        }

        var file = await exporter.ExportAsync(User.GetUserId(), year, month, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{id}")]
    public Task<TransactionDto> Get(string id, CancellationToken cancellationToken) =>
        transactions.GetByIdAsync(User.GetUserId(), id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(TransactionInput input, CancellationToken cancellationToken)
    {
        var transaction = await transactions.CreateAsync(User.GetUserId(), input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = transaction.Id }, transaction);
    }

    [HttpPut("{id}")]
    public Task<TransactionDto> Update(string id, TransactionInput input, CancellationToken cancellationToken) =>
        transactions.UpdateAsync(User.GetUserId(), id, input, cancellationToken);

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await transactions.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
