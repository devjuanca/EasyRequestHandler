using System.Threading;
using System.Threading.Tasks;

namespace EasyRequestHandlers.Request
{
    /// <summary>
    /// Represents a base class for request handlers that handle requests with a specified request and response type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public abstract class RequestHandler<TRequest, TResponse> : BaseHandler
    {

        /// <summary>
        /// Handles the request asynchronously and returns a response.
        /// </summary>
        /// <param name="request">The request to be handled.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation, with a response of type <typeparamref name="TResponse"/>.</returns>
        public abstract Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }


    /// <summary>
    /// Represents a base class for request handlers that handle requests without a specific request type, only a response type.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public abstract class RequestHandler<TResponse> : BaseHandler
    {
        /// <summary>
        /// Handles the request asynchronously and returns a response.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation, with a response of type <typeparamref name="TResponse"/>.</returns>
        public abstract Task<TResponse> HandleAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a base class for request handlers that process a request without returning a meaningful response.
    /// Useful for command-style operations where the caller only needs to know whether the operation completed successfully.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    public abstract class VoidRequestHandler<TRequest> : BaseHandler
    {
        /// <summary>
        /// Handles the request asynchronously without returning a response.
        /// </summary>
        /// <param name="request">The request to be handled.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }
}