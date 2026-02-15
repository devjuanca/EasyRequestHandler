using System.Threading;
using System.Threading.Tasks;

namespace EasyRequestHandlers.Request
{
    /// <summary>
    /// Defines a hook that executes both before and after a request is handled.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IRequestHook<TRequest, TResponse>
    {
        /// <summary>
        /// Called before the request handler is executed.
        /// </summary>
        /// <param name="request">The request being processed.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnExecutingAsync(TRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Called after the request handler has been executed.
        /// </summary>
        /// <param name="request">The request that was processed.</param>
        /// <param name="response">The response returned by the handler.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnExecutedAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Defines a hook that executes before a request is handled.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    public interface IRequestPreHook<TRequest>
    {
        /// <summary>
        /// Called before the request handler is executed.
        /// </summary>
        /// <param name="request">The request being processed.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnExecutingAsync(TRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Defines a hook that executes after a request has been handled.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public interface IRequestPostHook<TRequest, TResponse>
    {
        /// <summary>
        /// Called after the request handler has been executed.
        /// </summary>
        /// <param name="request">The request that was processed.</param>
        /// <param name="response">The response returned by the handler.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task OnExecutedAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default);
    }
}