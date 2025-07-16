
using SampleAPI.Dtos;

namespace SampleAPI.Behaviors;

public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
    {
        if (request is CreateCityForecastCommand and { City: "" or null })
        {
            throw new ArgumentException("City name cannot be empty");
        }

        return next();
    }
}
