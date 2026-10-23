using MAiMoney.Services.Entities;
using MAiMoney.Services.Models;
using MAiMoney.Services.Services;
using MAiMoney.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAiMoney.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> GetAll([FromQuery] TransactionType? type, CancellationToken cancellationToken) =>
        categories.GetAllAsync(User.GetUserId(), type, cancellationToken);

    [HttpGet("{id}")]
    public Task<CategoryDto> Get(string id, CancellationToken cancellationToken) =>
        categories.GetByIdAsync(User.GetUserId(), id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryInput input, CancellationToken cancellationToken)
    {
        var category = await categories.CreateAsync(User.GetUserId(), input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, category);
    }

    [HttpPut("{id}")]
    public Task<CategoryDto> Update(string id, CategoryInput input, CancellationToken cancellationToken) =>
        categories.UpdateAsync(User.GetUserId(), id, input, cancellationToken);

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await categories.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
