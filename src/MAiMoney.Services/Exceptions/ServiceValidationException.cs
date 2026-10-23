namespace MAiMoney.Services.Exceptions;

/// <summary>Thrown when input breaks a business rule. The message is safe to show to the user.</summary>
public class ServiceValidationException(string message) : Exception(message);
