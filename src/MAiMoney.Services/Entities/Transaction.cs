using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MAiMoney.Services.Entities;

public class Transaction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string CategoryId { get; set; } = string.Empty;

    public TransactionType Type { get; set; }

    /// <summary>Always positive; whether it adds or subtracts comes from <see cref="Type"/>.</summary>
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    /// <summary>The day of the transaction, stored as UTC midnight.</summary>
    public DateTime Date { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
