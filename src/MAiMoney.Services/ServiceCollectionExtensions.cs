using MAiMoney.Services.Data;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Options;
using MAiMoney.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MAiMoney.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the options, the Mongo context and the business services.</summary>
    public static IServiceCollection AddMAiMoneyServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MAiMoneyOptions>()
            .Bind(configuration.GetSection(MAiMoneyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<MongoDbContext>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();

        return services;
    }
}
