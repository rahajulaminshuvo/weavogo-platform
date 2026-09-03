using FluentValidation;
using MediatR;

namespace Weavo.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behaviour that runs every registered
/// <see cref="IValidator{T}"/> for a request before its handler executes.
/// </summary>
/// <typeparam name="TRequest">The command or query being handled.</typeparam>
/// <typeparam name="TResponse">The handler's return type.</typeparam>
/// <remarks>
/// <para>
/// All validators for the request run together, so the caller sees every
/// problem at once rather than fixing them one round trip at a time.
/// </para>
/// <para>
/// Lives in BuildingBlocks rather than in a single service: the behaviour is
/// identical for every bounded context, and one copy means one place to change
/// how validation failures surface across the platform.
/// </para>
/// <para>
/// Register it after <c>LoggingBehavior</c> so the logged duration includes
/// validation, and before any unit-of-work behaviour so a rejected request
/// never opens a transaction:
/// </para>
/// <code>
/// cfg.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;));     // outermost
/// cfg.AddOpenBehavior(typeof(ValidationBehavior&lt;,&gt;));
/// </code>
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Creates the behaviour.</summary>
    /// <param name="validators">
    /// Validators registered for this request type; empty when none exist.
    /// </param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _validators = validators;
    }

    /// <inheritdoc />
    /// <exception cref="ValidationException">
    /// Thrown when any validator reports a failure. The API edge translates this
    /// into a 400 response carrying the per-property messages.
    /// </exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Materialised once: the injected enumerable may be a lazy DI query, and
        // Any() followed by Select() would otherwise resolve it twice.
        var validators = _validators as IValidator<TRequest>[] ?? _validators.ToArray();

        if (validators.Length == 0)
        {
            // Queries generally have no validator; pass straight through.
#pragma warning disable CA2016 // RequestHandlerDelegate accepts no CancellationToken in MediatR 12.
            return await next().ConfigureAwait(false);
#pragma warning restore CA2016
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(validator =>
                validator.ValidateAsync(context, cancellationToken)))
            .ConfigureAwait(false);

        var failures = results
            .Where(result => !result.IsValid)
            .SelectMany(result => result.Errors)
            .ToArray();

        if (failures.Length > 0)
        {
            throw new ValidationException(failures);
        }

#pragma warning disable CA2016 // RequestHandlerDelegate accepts no CancellationToken in MediatR 12.
        return await next().ConfigureAwait(false);
#pragma warning restore CA2016
    }
}
