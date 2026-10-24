using EphemeralMongo;
using MAiMoney.Services.Data;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Options;
using MAiMoney.Services.Services;
using Microsoft.AspNetCore.Identity;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace MAiMoney.Services.Tests;

/// <summary>
/// Starts a throw-away MongoDB server (EphemeralMongo) shared by every test in the <see cref="MongoCollection"/>.
/// The mongod binaries are downloaded on the first run; set MAIMONEY_TEST_MONGO_BIN to a folder
/// with your own mongod to skip the download (e.g. offline or behind a proxy).
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private IMongoRunner? runner;

    public MongoDbContext Db { get; private set; } = null!;

    public CategoryService Categories { get; private set; } = null!;

    public TransactionService Transactions { get; private set; } = null!;

    public UserService Users { get; private set; } = null!;

    /// <summary>The default categories every registered user gets.</summary>
    public static List<DefaultCategoryOptions> DefaultCategories =>
    [
        new() { Name = "Salary", Type = TransactionType.Income },
        new() { Name = "Food", Type = TransactionType.Expense, Description = "Groceries" },
        new() { Name = "Rent", Type = TransactionType.Expense }
    ];

    public async Task InitializeAsync()
    {
        var options = new MongoRunnerOptions { AdditionalArguments = ["--quiet"] };
        var binaryDirectory = Environment.GetEnvironmentVariable("MAIMONEY_TEST_MONGO_BIN");
        if (!string.IsNullOrWhiteSpace(binaryDirectory))
        {
            // Only set when given: EphemeralMongo rejects a null directory instead of downloading.
            options.BinaryDirectory = binaryDirectory;
        }

        runner = await MongoRunner.RunAsync(options);

        Db = new MongoDbContext(MsOptions.Create(new MongoDbOptions
        {
            ConnectionString = runner.ConnectionString,
            DatabaseName = "MAiMoneyTests"
        }));
        await Db.EnsureIndexesAsync();

        Categories = CreateCategoryService(DefaultCategories);
        Transactions = new TransactionService(Db);
        Users = new UserService(Db, new PasswordHasher<User>(), Categories);
    }

    public Task DisposeAsync()
    {
        // Stops mongod and deletes its data directory.
        runner?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>A category service on the shared database with its own default categories.</summary>
    public CategoryService CreateCategoryService(List<DefaultCategoryOptions> defaultCategories) =>
        new(Db, MsOptions.Create(new MAiMoneyOptions { DefaultCategories = defaultCategories }));

    /// <summary>Registers a fresh user and returns its id.</summary>
    public async Task<string> NewUserAsync()
    {
        var user = await Users.RegisterAsync(NewEmail(), "Test User", "password123");
        return user.Id;
    }

    /// <summary>An email address no other test uses.</summary>
    public static string NewEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@example.test";
}

[CollectionDefinition(Name)]
public class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "Mongo";
}
