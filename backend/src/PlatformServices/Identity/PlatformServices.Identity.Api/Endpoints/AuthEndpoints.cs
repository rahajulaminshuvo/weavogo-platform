namespace PlatformServices.Identity.Api.Endpoints;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using PlatformServices.Identity.Application.DTOs;
using PlatformServices.Identity.Application.Features.Authentication.GenerateToken;

/// <summary>Token issuance endpoints.</summary>
/// <remarks>
/// Thin by design: bind, send a MediatR message, map the outcome. The fallback
/// authorization policy protects everything by default, so these two endpoints
/// opt out explicitly — a caller has no token yet.
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>Registers the authentication routes.</summary>
    /// <param name="app">Route builder to register on.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        group.MapPost("/token", GenerateTokenAsync)
            .AllowAnonymous()
            .WithName("GenerateToken")
            .WithSummary("Exchanges credentials for a signed access token.")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> GenerateTokenAsync(
        [FromBody] TokenGenerationRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender
            .Send(new GenerateTokenCommand(request), cancellationToken)
            .ConfigureAwait(false);

        // One undifferentiated 401 for every failure. Distinguishing "no such
        // user" from "wrong password" would let an attacker enumerate accounts.
        return result is null ? Results.Unauthorized() : Results.Ok(result);
    }
}
