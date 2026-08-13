using FluentValidation;
using MediatR;
using ValidationException = BudgetGuard.Application.Common.Exceptions.ValidationException;

namespace BudgetGuard.Application.Common.Behaviours;

/// <summary>
/// Runs every registered validator for a request before its handler executes.
/// <para>
/// Putting validation in the pipeline rather than in handlers means a handler
/// can assume its request is already valid, and means no caller — API,
/// Blazor, or a future background job — can skip validation by forgetting to
/// call it. Adding a validator class is enough to enforce a rule everywhere.
/// </para>
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length != 0)
        {
            throw new ValidationException(failures
                .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
                .ToDictionary(g => g.Key, g => g.ToArray()));
        }

        return await next();
    }
}
