using EasyRequestHandlers.Request;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Benchmarks.Behaviors;

// MediatR logging behavior
public class MediatRLoggingBehavior<TRequest, TResponse>(ILogger<MediatRLoggingBehavior<TRequest, TResponse>> logger) : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}

// EasyRequestHandler logging behavior
public class EasyLoggingBehavior<TRequest, TResponse>(ILogger<EasyLoggingBehavior<TRequest, TResponse>> logger) : EasyRequestHandlers.Request.IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, EasyRequestHandlers.Request.RequestHandlerDelegate<TResponse> next)
    {
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}