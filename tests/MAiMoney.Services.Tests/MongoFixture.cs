using MAiMoney.Services.Data;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Options;
using MAiMoney.Services.Services;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace MAiMoney.Services.Tests;

/// <summary>
/// Integration tests run against a real MongoDB (start it with `docker compose up -d mongo`).
/// Each test run gets its own throw-away database. Override the server with MAIMONEY_TEST_MONGO.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly string databaseName = $"MAiMoneyTests_{Guid.NewGuid():N}";

    public MongoFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("MAIMONEY_TEST_MONGO") ?? "mongodb://localhost:27017";
        Db = new MongoDbContext(MsOptions.Create(new MongoDbOptions
        {
            ConnectionString = connectionString,
            DatabaseName = databaseName
        }));

        var options = MsOptions.Create(new MAiMoneyOptions
        {
            DefaultCategories =
            [
                new() { Name = "Salary", Type = TransactionType.Income },
                new() { Name = "Food", Type = TransactionType.Expense, Description = "Groceries" },
                new() { Name = "Rent", Type = TransactionType.Expense }
            ]
        });

        Categories = new CategoryService(Db, options);
        Transactions = new TransactionService(Db);
        Users = new UserService(Db, new PasswordHasher<User>(), Categories);
    }

    public MongoDbContext Db { get; }

    public CategoryService Categories { get; }

    public TransactionService Transactions { get; }

    public UserService Users { get; }

    /// <summary>Registers a fresh user and returns its id.</summary>
    public async Task<string> NewUserAsync()
    {
        var user = await Users.RegisterAsync($"user-{Guid.NewGuid():N}@example.test", "Test User", "password123");
        return user.Id;
    }

    public Task InitializeAsync() => Db.EnsureIndexesAsync();

    public Task DisposeAsync() => Db.Database.Client.DropDatabaseAsync(databaseName);
}

[CollectionDefinition(Name)]
public class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "Mongo";
}
