using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyRequestHandlers.Request
{
    /// <summary>
    /// Provides methods for sending requests to their corresponding handlers using the mediator pattern.
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Sends a request to its corresponding handler and returns the response.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="request">The request instance to send.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation, containing the response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
        Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a request without input parameters to its corresponding handler and returns the response.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation, containing the response.</returns>
        Task<TResponse> SendAsync<TResponse>(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of ISender that handles request dispatching with support for behaviors and hooks.
    /// </summary>
    public class Sender : ISender
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RequestHandlerOptions _options;
        private readonly ILogger<Sender> _logger;

        /// <summary>
        /// Initializes a new instance of the Sender class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
        /// <param name="options">Configuration options for request handling.</param>
        public Sender(IServiceProvider serviceProvider, RequestHandlerOptions options)
            : this(serviceProvider, options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Sender class with optional logging.
        /// </summary>
        /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
        /// <param name="options">Configuration options for request handling.</param>
        /// <param name="logger">Optional logger for observability.</param>
        public Sender(IServiceProvider serviceProvider, RequestHandlerOptions options, ILogger<Sender> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Processing request of type {RequestType}", typeof(TRequest).Name);
            }

            var handler = _serviceProvider.GetRequiredService<RequestHandler<TRequest, TResponse>>();

            // Fast path: no behaviors and no hooks — skip all pipeline overhead
            if (!_options.HasBehaviors && !_options.EnableRequestHooks)
            {
                return handler.HandleAsync(request, cancellationToken);
            }

            if (!_options.EnableRequestHooks)
            {
                return ExecuteWithBehaviorsOnly(handler, request, cancellationToken);
            }

            return ExecuteWithFullPipeline(handler, request, cancellationToken);
        }

        public Task<TResponse> SendAsync<TResponse>(CancellationToken cancellationToken = default)
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Processing no-input request for response type {ResponseType}", typeof(TResponse).Name);
            }

            var handler = _serviceProvider.GetRequiredService<RequestHandler<TResponse>>();

            // Fast path: no behaviors and no hooks
            if (!_options.HasBehaviors && !_options.EnableRequestHooks)
            {
                return handler.HandleAsync(cancellationToken);
            }

            if (!_options.EnableRequestHooks)
            {
                return ExecuteWithBehaviorsOnlyForEmpty(handler, EmptyRequest.Instance, cancellationToken);
            }

            return ExecuteWithFullPipelineForEmpty(handler, EmptyRequest.Instance, cancellationToken);
        }

        private Task<TResponse> ExecuteWithBehaviorsOnly<TRequest, TResponse>(
            RequestHandler<TRequest, TResponse> handler,
            TRequest request,
            CancellationToken cancellationToken)
        {
            var behaviorServices = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

            if (behaviorServices.Length == 0)
            {
                return handler.HandleAsync(request, cancellationToken);
            }

            RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(request, cancellationToken);

            for (int i = behaviorServices.Length - 1; i >= 0; i--)
            {
                var behavior = behaviorServices[i];
                var next = pipeline;
                pipeline = () => behavior.Handle(request, cancellationToken, next);
            }

            return pipeline();
        }

        private Task<TResponse> ExecuteWithFullPipeline<TRequest, TResponse>(
            RequestHandler<TRequest, TResponse> handler,
            TRequest request,
            CancellationToken cancellationToken)
        {
            var behaviorsArray = _options.HasBehaviors
                ? _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray()
                : Array.Empty<IPipelineBehavior<TRequest, TResponse>>();

            var hooksArray = _serviceProvider.GetServices<IRequestHook<TRequest, TResponse>>().ToArray();
            var preHooksArray = _serviceProvider.GetServices<IRequestPreHook<TRequest>>().ToArray();
            var postHooksArray = _serviceProvider.GetServices<IRequestPostHook<TRequest, TResponse>>().ToArray();

            if (behaviorsArray.Length == 0 && hooksArray.Length == 0 && preHooksArray.Length == 0 && postHooksArray.Length == 0)
            {
                return handler.HandleAsync(request, cancellationToken);
            }

            RequestHandlerDelegate<TResponse> pipeline = async () =>
            {
                for (int i = 0; i < preHooksArray.Length; i++)
                {
                    try
                    {
                        await preHooksArray[i].OnExecutingAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger?.LogError(ex, "Error executing pre-hook {HookType} for request {RequestType}",
                            preHooksArray[i].GetType().Name, typeof(TRequest).Name);
                        throw;
                    }
                }

                for (int i = 0; i < hooksArray.Length; i++)
                {
                    try
                    {
                        await hooksArray[i].OnExecutingAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger?.LogError(ex, "Error executing hook (pre-phase) {HookType} for request {RequestType}",
                            hooksArray[i].GetType().Name, typeof(TRequest).Name);
                        throw;
                    }
                }

                var response = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

                for (int i = 0; i < postHooksArray.Length; i++)
                {
                    try
                    {
                        await postHooksArray[i].OnExecutedAsync(request, response, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger?.LogError(ex, "Error executing post-hook {HookType} for request {RequestType}",
                            postHooksArray[i].GetType().Name, typeof(TRequest).Name);
                        throw;
                    }
                }

                for (int i = 0; i < hooksArray.Length; i++)
                {
                    try
                    {
                        await hooksArray[i].OnExecutedAsync(request, response, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger?.LogError(ex, "Error executing hook (post-phase) {HookType} for request {RequestType}",
                            hooksArray[i].GetType().Name, typeof(TRequest).Name);
                        throw;
                    }
                }

                return response;
            };

            for (int i = behaviorsArray.Length - 1; i >= 0; i--)
            {
                var behavior = behaviorsArray[i];
                var next = pipeline;
                pipeline = () => behavior.Handle(request, cancellationToken, next);
            }

            return pipeline();
        }

        private Task<TResponse> ExecuteWithBehaviorsOnlyForEmpty<TResponse>(
            RequestHandler<TResponse> handler,
            EmptyRequest emptyRequest,
            CancellationToken cancellationToken)
        {
            var behaviorServices = _serviceProvider.GetServices<IPipelineBehavior<EmptyRequest, TResponse>>().ToArray();

            if (behaviorServices.Length == 0)
            {
                return handler.HandleAsync(cancellationToken);
            }

            RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(cancellationToken);

            for (int i = behaviorServices.Length - 1; i >= 0; i--)
            {
                var behavior = behaviorServices[i];
                var next = pipeline;
                pipeline = () => behavior.Handle(emptyRequest, cancellationToken, next);
            }

            return pipeline();
        }

        private Task<TResponse> ExecuteWithFullPipelineForEmpty<TResponse>(
            RequestHandler<TResponse> handler,
            EmptyRequest emptyRequest,
            CancellationToken cancellationToken)
        {
            var behaviorsArray = _options.HasBehaviors
                ? _serviceProvider.GetServices<IPipelineBehavior<EmptyRequest, TResponse>>().ToArray()
                : Array.Empty<IPipelineBehavior<EmptyRequest, TResponse>>();

            var hooksArray = _serviceProvider.GetServices<IRequestHook<EmptyRequest, TResponse>>().ToArray();
            var preHooksArray = _serviceProvider.GetServices<IRequestPreHook<EmptyRequest>>().ToArray();
            var postHooksArray = _serviceProvider.GetServices<IRequestPostHook<EmptyRequest, TResponse>>().ToArray();

            if (behaviorsArray.Length == 0 && hooksArray.Length == 0 && preHooksArray.Length == 0 && postHooksArray.Length == 0)
            {
                return handler.HandleAsync(cancellationToken);
            }

            RequestHandlerDelegate<TResponse> pipeline = async () =>
            {
                for (int i = 0; i < preHooksArray.Length; i++)
                {
                    try
                    {
                        await preHooksArray[i].OnExecutingAsync(emptyRequest, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger?.LogError(ex, "Error executing pre-hook {HookType} for no-input request",
                            preHooksArray[i].GetType().Name);
                        throw;
                    }
                }

                for (int i = 0; i < hooksArray.Length; i++)
                {
                    try
                    {
                        await hooksArray[i].OnExecutingAsync(emptyRequest, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger?.LogError(ex, "Error executing hook (pre-phase) {HookType} for no-input request",
                            hooksArray[i].GetType().Name);
                        throw;
                    }
                }

                var response = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

                for (int i = 0; i < postHooksArray.Length; i++)
                {
                    try
                    {
                        await postHooksArray[i].OnExecutedAsync(emptyRequest, response, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger?.LogError(ex, "Error executing post-hook {HookType} for no-input request",
                            postHooksArray[i].GetType().Name);
                        throw;
                    }
                }

                for (int i = 0; i < hooksArray.Length; i++)
                {
                    try
                    {
                        await hooksArray[i].OnExecutedAsync(emptyRequest, response, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger?.LogError(ex, "Error executing hook (post-phase) {HookType} for no-input request",
                            hooksArray[i].GetType().Name);
                        throw;
                    }
                }

                return response;
            };

            for (int i = behaviorsArray.Length - 1; i >= 0; i--)
            {
                var behavior = behaviorsArray[i];
                var next = pipeline;
                pipeline = () => behavior.Handle(emptyRequest, cancellationToken, next);
            }

            return pipeline();
        }
    }
}
