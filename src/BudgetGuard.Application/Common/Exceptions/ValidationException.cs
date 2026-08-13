namespace BudgetGuard.Application.Common.Exceptions;

/// <summary>
/// Thrown when a command fails its FluentValidation rules.
/// <para>
/// Carries errors grouped by property so the API can return an RFC 7807
/// problem document and Blazor can put each message next to its field, without
/// either of them re-deriving validation logic.
/// </para>
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures occurred.") =>
        Errors = errors;

    public IDictionary<string, string[]> Errors { get; }

    /// <summary>Every message, flattened — for surfaces that just want a list.</summary>
    public IEnumerable<string> AllMessages => Errors.SelectMany(e => e.Value);
}

/// <summary>Thrown when a requested entity does not exist.</summary>
public sealed class NotFoundException(string name, object key)
    : Exception($"{name} \"{key}\" was not found.");
