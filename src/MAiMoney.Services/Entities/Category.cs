using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MAiMoney.Services.Entities;

public class Category
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Trimmed, upper-cased name used for the per-user uniqueness check.</summary>
    public string NameNormalized { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>Hex color such as "#4caf50".</summary>
    public string? Color { get; set; }

    /// <summary>Optional monthly spending limit; only used for expense categories.</summary>
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? MonthlyBudget { get; set; }

    public DateTime CreatedAt { get; set; }
}
