namespace GoGoaRentals.Services.Common;

/// <summary>
/// Expected business failures. The API maps them to ProblemDetails responses
/// (404, 409, 422) and their messages are safe to show to the user.
/// </summary>
public abstract class ServiceException(string title, string message) : Exception(message)
{
    public string Title { get; } = title;
}

public sealed class NotFoundException(string what)
    : ServiceException("Not found", $"{what} was not found.");

public class ConflictException(string message) : ServiceException("Conflict", message);

/// <summary>The request is well-formed but not allowed in the current state.</summary>
public sealed class BusinessRuleException(string message) : ServiceException("Not allowed", message);
