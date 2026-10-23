using MAiMoney.Services.Entities;
using MAiMoney.Services.Options;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace MAiMoney.Services.Data;

/// <summary>Holds the Mongo client and the typed collections. Registered as a singleton.</summary>
public sealed class MongoDbContext
{
    static MongoDbContext()
    {
        var conventions = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true)
        };
        ConventionRegistry.Register(
            "MAiMoney",
            conventions,
            type => type.Namespace?.StartsWith("MAiMoney.Services.Entities", StringComparison.Ordinal) == true);
    }

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        Database = client.GetDatabase(options.Value.DatabaseName);
        Users = Database.GetCollection<User>("users");
        Categories = Database.GetCollection<Category>("categories");
        Transactions = Database.GetCollection<Transaction>("transactions");
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users { get; }

    public IMongoCollection<Category> Categories { get; }

    public IMongoCollection<Transaction> Transactions { get; }

    /// <summary>Creates the indexes the services rely on. Safe to call on every startup.</summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await Categories.Indexes.CreateOneAsync(
            new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys
                    .Ascending(c => c.UserId)
                    .Ascending(c => c.Type)
                    .Ascending(c => c.NameNormalized),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);

        await Transactions.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<Transaction>(
                    Builders<Transaction>.IndexKeys.Ascending(t => t.UserId).Descending(t => t.Date)),
                new CreateIndexModel<Transaction>(
                    Builders<Transaction>.IndexKeys.Ascending(t => t.UserId).Ascending(t => t.CategoryId))
            ],
            cancellationToken);
    }
}
