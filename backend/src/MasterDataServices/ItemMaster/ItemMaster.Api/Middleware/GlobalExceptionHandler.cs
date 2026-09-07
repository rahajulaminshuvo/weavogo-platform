namespace ItemMaster.Api.Middleware;

using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Translates unhandled exceptions into RFC 7807 <see cref="ProblemDetails"/>.
/// </summary>
/// <remarks>
/// Centralising this lets handlers and the domain throw freely rather than each
/// returning a hand-rolled result envelope. Only known exception types reveal
/// their message; anything else is reported generically, so an unexpected
/// failure cannot leak a connection string or stack trace to a caller.
/// </remarks>
/// <param name="logger">Logger for unhandled failures.</param>
public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        LogUnhandledException(logger, exception);

        // Headers already sent means the response is committed; overwriting the
        // status now would corrupt it. Returning false lets the host abort.
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path,
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Title = "Validation Failed";
            problemDetails.Detail = "One or more validation errors occurred.";

            // Per-property messages, so a client can attach each one to the
            // field that produced it. Shape matches ASP.NET's own
            // ValidationProblemDetails, which clients already know how to read.
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName, error => error.ErrorMessage)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        }
        else
        {
            var (statusCode, title, detail) = exception switch
            {
                // Domain and argument violations: well-formed request, but it
                // conflicts with a business rule.
                ArgumentException or InvalidOperationException
                    => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message),

                KeyNotFoundException
                    => (StatusCodes.Status404NotFound, "Not Found", exception.Message),

                UnauthorizedAccessException
                    => (StatusCodes.Status401Unauthorized,
                        "Unauthorized",
                        "Authentication is required."),

                // Optimistic concurrency: another writer won the race.
                DbUpdateConcurrencyException
                    => (StatusCodes.Status409Conflict,
                        "Conflict",
                        "The record was modified by another user. Reload and retry."),

                // Anything unrecognised is reported opaquely: the detail is in
                // the log, not the response.
                _ => (StatusCodes.Status500InternalServerError,
                      "Internal Server Error",
                      "An unexpected error occurred while processing the request."),
            };

            problemDetails.Status = statusCode;
            problemDetails.Title = title;
            problemDetails.Detail = detail;
        }

        // Ties the failure back to its distributed trace.
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        // Status is assigned on every branch above, so the null-forgiving read
        // is safe; using it keeps the two values from drifting apart.
        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        await httpContext.Response
            .WriteAsJsonAsync(problemDetails, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    // Source-generated, required by CA1848 which Directory.Build.props promotes
    // to an error.
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Error,
        Message = "Unhandled exception occurred")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);
}
