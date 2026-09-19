namespace PlatformServices.Identity.Application.Features.Authentication.GenerateToken;

using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using PlatformServices.Identity.Application.Contracts;
using PlatformServices.Identity.Application.DTOs;
using PlatformServices.Identity.Domain;

/// <summary>Exchanges credentials for a token pair.</summary>
/// <param name="Request">Credentials and the requested scope.</param>
public sealed record GenerateTokenCommand(TokenGenerationRequest Request)
    : IRequest<TokenResponse?>;

/// <summary>Shape validation for <see cref="GenerateTokenCommand"/>.</summary>
/// <remarks>
/// Checks only what is knowable without a database lookup. Whether the
/// credentials are correct, the account is active, or the scope is legitimate is
/// the handler's business — reporting those as validation errors would tell an
/// attacker which usernames exist.
/// </remarks>
public sealed class GenerateTokenCommandValidator : AbstractValidator<GenerateTokenCommand>
{
    /// <summary>Declares the ruleset.</summary>
    public GenerateTokenCommandValidator()
    {
        RuleFor(x => x.Request.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(100);

        RuleFor(x => x.Request.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.Request.BusinessUnitId)
            .GreaterThan(0)
            .When(x => x.Request.BusinessUnitId.HasValue)
            .WithMessage("BusinessUnitId must be a positive identifier when supplied.");
    }
}

/// <summary>
/// Verifies credentials and issues a scoped token.
/// </summary>
/// <remarks>
/// <para>
/// Orchestrates two aggregates: <c>UserAccount</c> for identity and activation,
/// <c>UserCredential</c> for authentication state. Both are reached through
/// repositories — never a DbContext.
/// </para>
/// <para>
/// Every failure path returns null and the endpoint maps that to 401. The
/// reason is deliberately not distinguished for the caller: "no such user",
/// "wrong password" and "locked out" are indistinguishable from outside, so an
/// attacker cannot enumerate accounts. The reason is logged, not returned.
/// </para>
/// </remarks>
/// <param name="accounts">Account repository.</param>
/// <param name="credentials">Credential repository.</param>
/// <param name="contextResolver">Resolves the tenancy scope for the token.</param>
/// <param name="passwordHasher">Verifies the supplied password.</param>
/// <param name="tokenGenerator">Mints the token pair.</param>
/// <param name="logger">Logger for authentication outcomes.</param>
public sealed partial class GenerateTokenCommandHandler(
    IUserAccountRepository accounts,
    IUserCredentialRepository credentials,
    IUserContextResolver contextResolver,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ILogger<GenerateTokenCommandHandler> logger)
    : IRequestHandler<GenerateTokenCommand, TokenResponse?>
{
    /// <inheritdoc />
    public async Task<TokenResponse?> Handle(
        GenerateTokenCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        var account = await accounts
            .FindByUsernameAsync(request.Username, cancellationToken)
            .ConfigureAwait(false);

        if (account is null || !account.IsActive)
        {
            LogAuthenticationRejected(logger, request.Username, "unknown or inactive account");
            return null;
        }

        var credential = await credentials
            .FindByUserAccountIdAsync(account.Id, cancellationToken)
            .ConfigureAwait(false);

        if (credential?.PasswordHash is null
            || credential.CredentialType != CredentialTypes.Password)
        {
            LogAuthenticationRejected(logger, request.Username, "no password credential set");
            return null;
        }

        if (credential.IsLockedOut())
        {
            LogAuthenticationRejected(logger, request.Username, "credential locked out");
            return null;
        }

        if (!passwordHasher.Verify(request.Password, credential.PasswordHash))
        {
            credential.RecordFailedLogin();

            // Committed even on failure: the lockout counter is the whole point,
            // and RecordFailedLogin may have raised UserLockedOutDomainEvent,
            // which the Outbox interceptor picks up during this save.
            await credentials.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            LogAuthenticationRejected(logger, request.Username, "password mismatch");
            return null;
        }

        // Resolved before recording success: an account with no usable grant in
        // the requested scope must not consume a login, and must not have its
        // failure counter reset either.
        var userContext = await contextResolver
            .ResolveAsync(account, request.BusinessUnitId, cancellationToken)
            .ConfigureAwait(false);

        if (userContext is null)
        {
            LogAuthenticationRejected(logger, request.Username, "no role grant in requested scope");
            return null;
        }

        credential.RecordSuccessfulLogin();
        await credentials.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogAuthenticationSucceeded(logger, account.Id, userContext.BusinessUnitId);

        return tokenGenerator.GenerateToken(userContext);
    }

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Warning,
        Message = "Authentication rejected for {Username}: {Reason}")]
    private static partial void LogAuthenticationRejected(
        ILogger logger, string username, string reason);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Authenticated user {UserAccountId} scoped to business unit {BusinessUnitId}")]
    private static partial void LogAuthenticationSucceeded(
        ILogger logger, int userAccountId, int businessUnitId);
}
