using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

        private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, object>> _factoryCache = new ConcurrentDictionary<Type, Func<IServiceProvider, object>>();
        
        // Cache for empty arrays to avoid repeated allocations
        private static readonly object[] _emptyArray = Array.Empty<object>();

        public Sender(IServiceProvider serviceProvider, RequestHandlerOptions options, ILogger<Sender> logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            try
            {
                _logger?.LogDebug("Processing request of type {RequestType}", typeof(TRequest).Name);

                var handler = (RequestHandler<TRequest, TResponse>)GetHandler(typeof(RequestHandler<TRequest, TResponse>));

                if (!_options.EnableRequestHooks)
                {
                    var behaviorServices = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

                    if (!behaviorServices.Any())
                    {
                        return await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    
                    // Only behaviors, no hooks - simplified pipeline
                    return await ExecuteWithBehaviorsOnly(handler, behaviorServices, request, cancellationToken).ConfigureAwait(false);
                }

                // Full pipeline with hooks
                return await ExecuteWithFullPipeline<TRequest, TResponse>(handler, request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing request of type {RequestType}", typeof(TRequest).Name);
                throw;
            }
        }

        public async Task<TResponse> SendAsync<TResponse>(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogDebug("Processing no-input request for response type {ResponseType}", typeof(TResponse).Name);

                var handler = _serviceProvider.GetRequiredService<RequestHandler<TResponse>>();

                // Use singleton EmptyRequest instance
                var emptyRequest = EmptyRequest.Instance;

                if (!_options.EnableRequestHooks)
                {
                    var behaviorServices = _serviceProvider.GetServices<IPipelineBehavior<EmptyRequest, TResponse>>();

                    if (!behaviorServices.Any())
                    {
                        return await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
                    }

                    return await ExecuteWithBehaviorsOnlyForEmpty(handler, behaviorServices, emptyRequest, cancellationToken).ConfigureAwait(false);
                }

                // Full pipeline with hooks for EmptyRequest
                return await ExecuteWithFullPipelineForEmpty(handler, emptyRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing no-input request for response type {ResponseType}", typeof(TResponse).Name);
                throw;
            }
        }

        private Task<TResponse> ExecuteWithBehaviorsOnly<TRequest, TResponse>(
            RequestHandler<TRequest, TResponse> handler,
            IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors,
            TRequest request,
            CancellationToken cancellationToken)
        {
            RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(request, cancellationToken);
            
            foreach (var behavior in behaviors.Reverse())
            {
                var currentBehavior = behavior;
                var next = pipeline;
                pipeline = () => currentBehavior.Handle(request, cancellationToken, next);
            }
            
            return pipeline();
        }

        private Task<TResponse> ExecuteWithFullPipeline<TRequest, TResponse>(
            RequestHandler<TRequest, TResponse> handler,
            TRequest request,
            CancellationToken cancellationToken)
        {

            var behaviors = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

            var hooks = _serviceProvider.GetServices<IRequestHook<TRequest, TResponse>>();

            var preHooks = _serviceProvider.GetServices<IRequestPreHook<TRequest>>();

            var postHooks = _serviceProvider.GetServices<IRequestPostHook<TRequest, TResponse>>();

            var behaviorsList = behaviors.ToList();

            var hooksList = hooks.ToList();

            var preHooksList = preHooks.ToList();

            var postHooksList = postHooks.ToList();

            if (behaviorsList.Count == 0 && hooksList.Count == 0 && preHooksList.Count == 0 && postHooksList.Count == 0)
            {
                return handler.HandleAsync(request, cancellationToken);
            }

            RequestHandlerDelegate<TResponse> pipeline = async () =>
            {
                try
                {
                    // Execute pre-hooks
                    for (int i = 0; i < preHooksList.Count; i++)
                    {
                        try
                        {
                            await preHooksList[i].OnExecutingAsync(request, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing pre-hook {HookType} for request {RequestType}", 
                                preHooksList[i].GetType().Name, typeof(TRequest).Name);
                            throw;
                        }
                    }
                    
                    for (int i = 0; i < hooksList.Count; i++)
                    {
                        try
                        {
                            await hooksList[i].OnExecutingAsync(request, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing hook (pre-phase) {HookType} for request {RequestType}", 
                                hooksList[i].GetType().Name, typeof(TRequest).Name);
                            throw;
                        }
                    }

                    // Execute handler
                    var response = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

                    // Execute post-hooks
                    for (int i = 0; i < postHooksList.Count; i++)
                    {
                        try
                        {
                            await postHooksList[i].OnExecutedAsync(request, response, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing post-hook {HookType} for request {RequestType}", 
                                postHooksList[i].GetType().Name, typeof(TRequest).Name);
                            throw;
                        }
                    }
                    
                    for (int i = 0; i < hooksList.Count; i++)
                    {
                        try
                        {
                            await hooksList[i].OnExecutedAsync(request, response, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing hook (post-phase) {HookType} for request {RequestType}", 
                                hooksList[i].GetType().Name, typeof(TRequest).Name);
                            throw;
                        }
                    }

                    return response;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger?.LogError(ex, "Error in request pipeline for {RequestType}", typeof(TRequest).Name);
                    throw;
                }
            };

            // Apply behaviors in reverse order
            for (int i = behaviorsList.Count - 1; i >= 0; i--)
            {
                var behavior = behaviorsList[i];
                var next = pipeline;
                pipeline = () => behavior.Handle(request, cancellationToken, next);
            }

            return pipeline();
        }

        private Task<TResponse> ExecuteWithBehaviorsOnlyForEmpty<TResponse>(
            RequestHandler<TResponse> handler,
            IEnumerable<IPipelineBehavior<EmptyRequest, TResponse>> behaviors,
            EmptyRequest emptyRequest,
            CancellationToken cancellationToken)
        {
            RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(cancellationToken);
            
            foreach (var behavior in behaviors.Reverse())
            {
                var currentBehavior = behavior;
                var next = pipeline;
                pipeline = () => currentBehavior.Handle(emptyRequest, cancellationToken, next);
            }
            
            return pipeline();
        }

        private Task<TResponse> ExecuteWithFullPipelineForEmpty<TResponse>(
            RequestHandler<TResponse> handler,
            EmptyRequest emptyRequest,
            CancellationToken cancellationToken)
        {
            var behaviors = _serviceProvider.GetServices<IPipelineBehavior<EmptyRequest, TResponse>>();

            var hooks = _serviceProvider.GetServices<IRequestHook<EmptyRequest, TResponse>>();

            var preHooks = _serviceProvider.GetServices<IRequestPreHook<EmptyRequest>>();

            var postHooks = _serviceProvider.GetServices<IRequestPostHook<EmptyRequest, TResponse>>();

            var behaviorsList = behaviors.ToList();

            var hooksList = hooks.ToList();

            var preHooksList = preHooks.ToList();

            var postHooksList = postHooks.ToList();

            if (behaviorsList.Count == 0 && hooksList.Count == 0 && preHooksList.Count == 0 && postHooksList.Count == 0)
            {
                return handler.HandleAsync(cancellationToken);
            }

            RequestHandlerDelegate<TResponse> pipeline = async () =>
            {
                try
                {
                    // Execute pre-hooks
                    for (int i = 0; i < preHooksList.Count; i++)
                    {
                        try
                        {
                            await preHooksList[i].OnExecutingAsync(emptyRequest, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing pre-hook {HookType} for no-input request", 
                                preHooksList[i].GetType().Name);
                            throw;
                        }
                    }
                    
                    for (int i = 0; i < hooksList.Count; i++)
                    {
                        try
                        {
                            await hooksList[i].OnExecutingAsync(emptyRequest, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing hook (pre-phase) {HookType} for no-input request", 
                                hooksList[i].GetType().Name);
                            throw;
                        }
                    }

                    // Execute handler
                    var response = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

                    // Execute post-hooks
                    for (int i = 0; i < postHooksList.Count; i++)
                    {
                        try
                        {
                            await postHooksList[i].OnExecutedAsync(emptyRequest, response, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing post-hook {HookType} for no-input request", 
                                postHooksList[i].GetType().Name);
                            throw;
                        }
                    }
                    
                    for (int i = 0; i < hooksList.Count; i++)
                    {
                        try
                        {
                            await hooksList[i].OnExecutedAsync(emptyRequest, response, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error executing hook (post-phase) {HookType} for no-input request", 
                                hooksList[i].GetType().Name);
                            throw;
                        }
                    }

                    return response;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger?.LogError(ex, "Error in no-input request pipeline");
                    throw;
                }
            };

            // Apply behaviors in reverse order
            for (int i = behaviorsList.Count - 1; i >= 0; i--)
            {
                var behavior = behaviorsList[i];
                var next = pipeline;
                pipeline = () => behavior.Handle(emptyRequest, cancellationToken, next);
            }

            return pipeline();
        }

        private object GetHandler(Type type)
        {
            var factory = _factoryCache.GetOrAdd(type, CreateFactory);

            return factory(_serviceProvider);
        }

        private static Func<IServiceProvider, object> CreateFactory(Type type)
        {
            var providerParam = Expression.Parameter(typeof(IServiceProvider), "provider");

            var getServiceCall = Expression.Call(
                typeof(ServiceProviderServiceExtensions),
                nameof(ServiceProviderServiceExtensions.GetRequiredService),
                new[] { type },
                providerParam
            );

            var castResult = Expression.Convert(getServiceCall, typeof(object));
            var lambda = Expression.Lambda<Func<IServiceProvider, object>>(castResult, providerParam);

            return lambda.Compile();
        }
    }
}

