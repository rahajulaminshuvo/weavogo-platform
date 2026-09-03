using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Weavo.BuildingBlocks.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var traceId = Activity.Current?.TraceId.ToString();

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestName"] = requestName,
            ["TraceId"] = traceId,
        });

        logger.LogInformation("Handling {RequestName}", requestName);
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            var response = await next().ConfigureAwait(false);
            logger.LogInformation("Handled {RequestName} in {ElapsedMilliseconds:F1} ms", 
                requestName, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{RequestName} failed after {ElapsedMilliseconds:F1} ms", 
                requestName, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
            throw;
        }
    }
}
