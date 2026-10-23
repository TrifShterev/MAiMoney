namespace MAiMoney.Services.Exceptions;

/// <summary>Thrown when an entity does not exist or does not belong to the current user.</summary>
public class NotFoundException(string message) : Exception(message);
