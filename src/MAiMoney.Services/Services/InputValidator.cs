using System.ComponentModel.DataAnnotations;
using MAiMoney.Services.Exceptions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MAiMoney.Services.Services;

internal static class InputValidator
{
    /// <summary>Runs the DataAnnotations rules on <paramref name="input"/> and throws on the first failure.</summary>
    public static void Validate(object input)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true))
        {
            throw new ServiceValidationException(results[0].ErrorMessage ?? "The input is not valid.");
        }
    }

    public static bool IsValidId(string? id) => ObjectId.TryParse(id, out _);

    /// <summary>Trims the text and turns blank strings into null.</summary>
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static bool IsDuplicateKey(MongoWriteException ex) =>
        ex.WriteError?.Category == ServerErrorCategory.DuplicateKey;
}
