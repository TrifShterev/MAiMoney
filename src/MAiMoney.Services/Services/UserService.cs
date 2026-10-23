using System.ComponentModel.DataAnnotations;
using MAiMoney.Services.Data;
using MAiMoney.Services.Entities;
using MAiMoney.Services.Exceptions;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace MAiMoney.Services.Services;

public class UserService(MongoDbContext db, IPasswordHasher<User> passwordHasher, ICategoryService categories) : IUserService
{
    public const int MinPasswordLength = 8;

    public async Task<User> RegisterAsync(
        string email, string displayName, string password, CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        displayName = displayName?.Trim() ?? string.Empty;

        if (email.Length == 0 || !new EmailAddressAttribute().IsValid(email))
        {
            throw new ServiceValidationException("Please enter a valid email address.");
        }

        if (displayName.Length is 0 or > 50)
        {
            throw new ServiceValidationException("Please enter your name (at most 50 characters).");
        }

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            throw new ServiceValidationException($"Password must be at least {MinPasswordLength} characters.");
        }

        if (await db.Users.Find(u => u.Email == email).AnyAsync(cancellationToken))
        {
            throw DuplicateEmail();
        }

        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        try
        {
            await db.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (InputValidator.IsDuplicateKey(ex))
        {
            throw DuplicateEmail();
        }

        await categories.SeedDefaultsAsync(user.Id, cancellationToken);
        return user;
    }

    public async Task<User?> ValidateCredentialsAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        if (email.Length == 0 || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            await db.Users.UpdateOneAsync(
                u => u.Id == user.Id,
                Builders<User>.Update.Set(u => u.PasswordHash, user.PasswordHash),
                cancellationToken: cancellationToken);
        }

        return user;
    }

    private static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    private static ServiceValidationException DuplicateEmail() =>
        new("An account with this email already exists.");
}
