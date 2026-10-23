using MAiMoney.Services.Entities;
using MAiMoney.Services.Models;

namespace MAiMoney.Services.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(string userId, TransactionType? type = null, CancellationToken cancellationToken = default);

    Task<CategoryDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(string userId, CategoryInput input, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateAsync(string userId, string id, CategoryInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes the category. Refused while transactions still use it.</summary>
    Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>Creates the configured default categories for a new user.</summary>
    Task SeedDefaultsAsync(string userId, CancellationToken cancellationToken = default);
}
