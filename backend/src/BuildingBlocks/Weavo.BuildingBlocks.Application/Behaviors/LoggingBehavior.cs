using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Weavo.BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behaviour wrapping every request with structured start,
/// completion and failure logs, an elapsed-time measurement, and an
/// <see cref="Activity"/> span for distributed tracing.
/// </summary>
/// <typeparam name="TRequest">The command or query being handled.</typeparam>
/// <typeparam name="TResponse">The handler's return type.</typeparam>
/// <remarks>
/// <para>
/// Registered once per service, this applies to every handler, so individual
/// handlers stay free of logging and timing boilerplate.
/// </para>
/// <para>
/// <b>Ordering matters.</b> Register this outermost so it observes the entire
/// pipeline, including time spent validating and committing.
/// </para>
/// <para>
/// Messages use named placeholders rather than string interpolation so Serilog
/// records <c>RequestName</c> and <c>ElapsedMilliseconds</c> as queryable fields.
/// The request body is deliberately never logged: ERP commands routinely carry
/// payroll figures, banking details and negotiated pricing, none of which belong
/// in a log sink.
/// </para>
/// </remarks>
public sealed partial class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Activity source for CQRS spans. Register it during start-up with
    /// <c>tracing.AddSource(LoggingBehavior&lt;,&gt;.ActivitySourceName)</c>;
    /// without a listener every span is null and the behaviour degrades to
    /// logging only.
    /// </summary>
    public const string ActivitySourceName = "Weavo.Application.Cqrs";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>Creates the behaviour.</summary>
    /// <param name="logger">Logger scoped to the closed generic type.</param>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var requestName = typeof(TRequest).Name;

        // Child span under the inbound HTTP request, so the CQRS handler appears
        // as its own segment in Jaeger rather than vanishing into the parent.
        using var activity = ActivitySource.StartActivity(
            $"CQRS {requestName}", ActivityKind.Internal);

        activity?.SetTag("cqrs.request_type", typeof(TRequest).FullName);

        // Correlates log lines with the trace, so a slow entry links straight to
        // its span.
        var traceId = Activity.Current?.TraceId.ToString();

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestName"] = requestName,
            ["TraceId"] = traceId,
        });

        LogHandling(_logger, requestName);

        var startingTimestamp = Stopwatch.GetTimestamp();

        try
        {
            // MediatR 12's RequestHandlerDelegate takes no token: the pipeline
            // already closed over the one passed to Send, so it flows to the
            // handler without being forwarded here.
#pragma warning disable CA2016 // RequestHandlerDelegate accepts no CancellationToken in MediatR 12.
            var response = await next().ConfigureAwait(false);
#pragma warning restore CA2016

            var elapsedMs = Stopwatch
                .GetElapsedTime(startingTimestamp).TotalMilliseconds;

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("cqrs.elapsed_ms", elapsedMs);

            LogHandled(_logger, requestName, elapsedMs);

            return response;
        }
        catch (Exception exception)
        {
            var elapsedMs = Stopwatch
                .GetElapsedTime(startingTimestamp).TotalMilliseconds;

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.SetTag("cqrs.elapsed_ms", elapsedMs);

            // Log and rethrow. The pipeline observes the failure; translating it
            // into an HTTP response is the job of the exception-handling
            // middleware at the API edge.
            LogFailed(_logger, requestName, elapsedMs, exception);

            throw;
        }
    }

    // Source-generated log methods. The generator caches the message template
    // and avoids boxing the arguments, which matters on a path that runs for
    // every command and query in the platform. Required by CA1848, which
    // Directory.Build.props promotes to an error.

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Handling {RequestName}")]
    private static partial void LogHandling(ILogger logger, string requestName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Handled {RequestName} in {ElapsedMilliseconds:F1} ms")]
    private static partial void LogHandled(
        ILogger logger, string requestName, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "{RequestName} failed after {ElapsedMilliseconds:F1} ms")]
    private static partial void LogFailed(
        ILogger logger,
        string requestName,
        double elapsedMilliseconds,
        Exception exception);
}
