using MAiMoney.Services.Data;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using MAiMoney.Services.Models;
using MAiMoney.Services.Options;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MAiMoney.Services.Services;

public class CategoryService(MongoDbContext db, IOptions<MAiMoneyOptions> options) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        string userId, TransactionType? type = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Category>.Filter.Eq(c => c.UserId, userId);
        if (type is not null)
        {
            filter &= Builders<Category>.Filter.Eq(c => c.Type, type.Value);
        }

        var categories = await db.Categories.Find(filter)
            .SortBy(c => c.NameNormalized)
            .ToListAsync(cancellationToken);
        return categories.Select(CategoryDto.From).ToList();
    }

    public async Task<CategoryDto> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default) =>
        CategoryDto.From(await GetOwnedAsync(userId, id, cancellationToken));

    public async Task<CategoryDto> CreateAsync(string userId, CategoryInput input, CancellationToken cancellationToken = default)
    {
        Validate(input);
        var category = new Category
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        Apply(input, category);
        await EnsureUniqueNameAsync(userId, category, cancellationToken);

        try
        {
            await db.Categories.InsertOneAsync(category, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (InputValidator.IsDuplicateKey(ex))
        {
            throw DuplicateName(category);
        }

        return CategoryDto.From(category);
    }

    public async Task<CategoryDto> UpdateAsync(
        string userId, string id, CategoryInput input, CancellationToken cancellationToken = default)
    {
        Validate(input);
        var category = await GetOwnedAsync(userId, id, cancellationToken);

        if (category.Type != input.Type && await IsInUseAsync(userId, id, cancellationToken))
        {
            throw new ServiceValidationException(
                "The type of a category can't be changed while transactions use it.");
        }

        Apply(input, category);
        await EnsureUniqueNameAsync(userId, category, cancellationToken);

        try
        {
            await db.Categories.ReplaceOneAsync(
                c => c.Id == id && c.UserId == userId, category, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (InputValidator.IsDuplicateKey(ex))
        {
            throw DuplicateName(category);
        }

        return CategoryDto.From(category);
    }

    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var category = await GetOwnedAsync(userId, id, cancellationToken);
        if (await IsInUseAsync(userId, id, cancellationToken))
        {
            throw new ServiceValidationException(
                $"\"{category.Name}\" is used by transactions. Move or delete those transactions first.");
        }

        await db.Categories.DeleteOneAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
    }

    public async Task SeedDefaultsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var defaults = options.Value.DefaultCategories
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .DistinctBy(d => (d.Type, Normalize(d.Name)))
            .Select(d => new Category
            {
                UserId = userId,
                Name = d.Name.Trim(),
                NameNormalized = Normalize(d.Name),
                Description = InputValidator.Clean(d.Description),
                Type = d.Type,
                Color = d.Color,
                CreatedAt = now
            })
            .ToList();

        if (defaults.Count > 0)
        {
            await db.Categories.InsertManyAsync(defaults, cancellationToken: cancellationToken);
        }
    }

    private async Task<Category> GetOwnedAsync(string userId, string id, CancellationToken cancellationToken)
    {
        var category = InputValidator.IsValidId(id)
            ? await db.Categories.Find(c => c.Id == id && c.UserId == userId).FirstOrDefaultAsync(cancellationToken)
            : null;
        return category ?? throw new NotFoundException("Category not found.");
    }

    private Task<bool> IsInUseAsync(string userId, string categoryId, CancellationToken cancellationToken) =>
        db.Transactions.Find(t => t.UserId == userId && t.CategoryId == categoryId).AnyAsync(cancellationToken);

    private async Task EnsureUniqueNameAsync(string userId, Category category, CancellationToken cancellationToken)
    {
        var builder = Builders<Category>.Filter;
        var filter = builder.Eq(c => c.UserId, userId)
            & builder.Eq(c => c.Type, category.Type)
            & builder.Eq(c => c.NameNormalized, category.NameNormalized);
        if (!string.IsNullOrEmpty(category.Id))
        {
            filter &= builder.Ne(c => c.Id, category.Id);
        }

        var taken = await db.Categories.Find(filter).AnyAsync(cancellationToken);
        if (taken)
        {
            throw DuplicateName(category);
        }
    }

    private static void Validate(CategoryInput input)
    {
        InputValidator.Validate(input);
        if (input.Type == TransactionType.Income && input.MonthlyBudget is not null)
        {
            throw new ServiceValidationException("A monthly budget can only be set on expense categories.");
        }
    }

    private static void Apply(CategoryInput input, Category category)
    {
        category.Name = input.Name.Trim();
        category.NameNormalized = Normalize(input.Name);
        category.Description = InputValidator.Clean(input.Description);
        category.Type = input.Type;
        category.Color = InputValidator.Clean(input.Color)?.ToLowerInvariant();
        category.MonthlyBudget = input.MonthlyBudget;
    }

    private static string Normalize(string name) => name.Trim().ToUpperInvariant();

    private static ServiceValidationException DuplicateName(Category category) =>
        new($"You already have an {category.Type.ToString().ToLowerInvariant()} category named \"{category.Name}\".");
}
