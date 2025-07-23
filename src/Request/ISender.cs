using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace EasyRequestHandlers.Request
{
    public interface ISender
    {
        Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default);

        Task<TResponse> SendAsync<TResponse>(CancellationToken cancellationToken = default);
    }

    public class Sender : ISender
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly RequestHandlerOptions _options;

        private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, object>> _factoryCache = new ConcurrentDictionary<Type, Func<IServiceProvider, object>>();
        
        // Cache for empty arrays to avoid repeated allocations
        private static readonly object[] _emptyArray = Array.Empty<object>();

        public Sender(IServiceProvider serviceProvider, RequestHandlerOptions options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var handler = (RequestHandler<TRequest, TResponse>)GetHandler(typeof(RequestHandler<TRequest, TResponse>));

            if (!_options.EnableRequestHooks)
            {
                var behaviorServices = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

                if (!behaviorServices.Any())
                {
                    return handler.HandleAsync(request, cancellationToken);
                }
                
                // Only behaviors, no hooks - simplified pipeline
                return ExecuteWithBehaviorsOnly(handler, behaviorServices, request, cancellationToken);
            }

            // Full pipeline with hooks
            return ExecuteWithFullPipeline<TRequest, TResponse>(handler, request, cancellationToken);
        }

        public Task<TResponse> SendAsync<TResponse>(CancellationToken cancellationToken = default)
        {
            var handler = _serviceProvider.GetRequiredService<RequestHandler<TResponse>>();

            if (!_options.EnableRequestHooks)
            {
                var behaviorServices = _serviceProvider.GetServices<IPipelineBehavior<EmptyRequest, TResponse>>();

                if (!behaviorServices.Any())
                {
                    return handler.HandleAsync(cancellationToken);
                }
                
                var emptyRequest = new EmptyRequest();

                return ExecuteWithBehaviorsOnlyForEmpty(handler, behaviorServices, emptyRequest, cancellationToken);
            }

            // Full pipeline with hooks for EmptyRequest
            var emptyRequestFull = new EmptyRequest();

            return ExecuteWithFullPipelineForEmpty(handler, emptyRequestFull, cancellationToken);
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
                // Execute pre-hooks
                for (int i = 0; i < preHooksList.Count; i++)
                {
                    await preHooksList[i].OnExecutingAsync(request, cancellationToken).ConfigureAwait(false);
                }
                
                for (int i = 0; i < hooksList.Count; i++)
                {
                    await hooksList[i].OnExecutingAsync(request, cancellationToken).ConfigureAwait(false);
                }

                // Execute handler
                var response = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

                // Execute post-hooks
                for (int i = 0; i < postHooksList.Count; i++)
                {
                    await postHooksList[i].OnExecutedAsync(request, response, cancellationToken).ConfigureAwait(false);
                }
                
                for (int i = 0; i < hooksList.Count; i++)
                {
                    await hooksList[i].OnExecutedAsync(request, response, cancellationToken).ConfigureAwait(false);
                }

                return response;
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
                // Execute pre-hooks
                for (int i = 0; i < preHooksList.Count; i++)
                {
                    await preHooksList[i].OnExecutingAsync(emptyRequest, cancellationToken).ConfigureAwait(false);
                }
                
                for (int i = 0; i < hooksList.Count; i++)
                {
                    await hooksList[i].OnExecutingAsync(emptyRequest, cancellationToken).ConfigureAwait(false);
                }

                // Execute handler
                var response = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

                // Execute post-hooks
                for (int i = 0; i < postHooksList.Count; i++)
                {
                    await postHooksList[i].OnExecutedAsync(emptyRequest, response, cancellationToken).ConfigureAwait(false);
                }
                
                for (int i = 0; i < hooksList.Count; i++)
                {
                    await hooksList[i].OnExecutedAsync(emptyRequest, response, cancellationToken).ConfigureAwait(false);
                }

                return response;
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

