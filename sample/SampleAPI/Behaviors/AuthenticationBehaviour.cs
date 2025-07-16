
namespace SampleAPI.Behaviors;

public class AuthenticationBehavior<TRequest, TResponse>(ILogger<AuthenticationBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
    {
        logger.LogInformation("Behavior: Authenticating request: {Request}", request);

        return next();
    }
}
