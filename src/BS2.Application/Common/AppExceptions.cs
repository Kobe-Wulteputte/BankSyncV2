namespace BS2.Application.Common;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

public sealed class ValidationException(IDictionary<string, string[]> errors) : Exception("One or more validation errors occurred.")
{
    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }

    public IDictionary<string, string[]> Errors { get; } = errors;
}
