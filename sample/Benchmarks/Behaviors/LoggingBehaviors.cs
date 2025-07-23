using EasyRequestHandlers.Request;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Benchmarks.Behaviors;

// MediatR logging behavior

public class MediatRLoggingBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
   where TRequest : IRequest<TResponse>
{
    private readonly ILogger<MediatRLoggingBehavior<TRequest, TResponse>> _logger;

    public MediatRLoggingBehavior(ILogger<MediatRLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}


// EasyRequestHandler logging behavior
public class EasyLoggingBehavior<TRequest, TResponse> : EasyRequestHandlers.Request.IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<EasyLoggingBehavior<TRequest, TResponse>> _logger;

    public EasyLoggingBehavior(ILogger<EasyLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, EasyRequestHandlers.Request.RequestHandlerDelegate<TResponse> next)
    {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}