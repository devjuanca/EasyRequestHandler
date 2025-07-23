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

        private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, Array>> _behaviorsFactoryCache = new ConcurrentDictionary<Type, Func<IServiceProvider, Array>>();

        private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, Array>> _hooksFactoryCache = new ConcurrentDictionary<Type, Func<IServiceProvider, Array>>();

        private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, Array>> _preHooksFactoryCache = new ConcurrentDictionary<Type, Func<IServiceProvider, Array>>();

        private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, Array>> _postHooksFactoryCache = new ConcurrentDictionary<Type, Func<IServiceProvider, Array>>();

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

            var behaviors = GetBehaviors<TRequest, TResponse>();

            var hooks = _options.EnableRequestHooks ? GetHooks<TRequest, TResponse>() : Array.Empty<IRequestHook<TRequest, TResponse>>();

            var preHooks = _options.EnableRequestHooks ? GetPreHooks<TRequest>() : Array.Empty<IRequestPreHook<TRequest>>();

            var postHooks = _options.EnableRequestHooks ? GetPostHooks<TRequest, TResponse>() : Array.Empty<IRequestPostHook<TRequest, TResponse>>();

            if (behaviors.Length == 0 && hooks.Length == 0 && preHooks.Length == 0 && postHooks.Length == 0)
            {
                return handler.HandleAsync(request, cancellationToken);
            }

            RequestHandlerDelegate<TResponse> handlerDelegate = async () =>
            {
                if (_options.EnableRequestHooks)
                {
                    if (preHooks.Length > 0)
                    {
                        for (int i = 0; i < preHooks.Length; i++)
                        {
                            await preHooks[i].OnExecutingAsync(request, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (hooks.Length > 0)
                    {
                        for (int i = 0; i < hooks.Length; i++)
                        {
                            await hooks[i].OnExecutingAsync(request, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                var response = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);

                if (_options.EnableRequestHooks)
                {
                    if (postHooks.Length > 0)
                    {
                        for (int i = 0; i < postHooks.Length; i++)
                        {
                            await postHooks[i].OnExecutedAsync(request, response, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (hooks.Length > 0)
                    {
                        for (int i = 0; i < hooks.Length; i++)
                        {
                            await hooks[i].OnExecutedAsync(request, response, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                return response;
            };

            if (behaviors.Length > 0)
            {
                for (int i = behaviors.Length - 1; i >= 0; i--)
                {
                    var next = handlerDelegate;
                    var behavior = behaviors[i];
                    handlerDelegate = () => behavior.Handle(request, cancellationToken, next);
                }
            }

            return handlerDelegate();
        }

        public Task<TResponse> SendAsync<TResponse>(CancellationToken cancellationToken = default)
        {
            var handler = _serviceProvider.GetRequiredService<RequestHandler<TResponse>>();

            var behaviors = GetBehaviors<EmptyRequest, TResponse>();

            var hooks = _options.EnableRequestHooks ? GetHooks<EmptyRequest, TResponse>() : Array.Empty<IRequestHook<EmptyRequest, TResponse>>();

            var preHooks = _options.EnableRequestHooks ? GetPreHooks<EmptyRequest>() : Array.Empty<IRequestPreHook<EmptyRequest>>();

            var postHooks = _options.EnableRequestHooks ? GetPostHooks<EmptyRequest, TResponse>() : Array.Empty<IRequestPostHook<EmptyRequest, TResponse>>();

            if (behaviors.Length == 0 && hooks.Length == 0 && preHooks.Length == 0 && postHooks.Length == 0)
            {
                return handler.HandleAsync(cancellationToken);
            }

            var emptyRequest = new EmptyRequest();

            RequestHandlerDelegate<TResponse> handlerDelegate = async () =>
            {
                if (_options.EnableRequestHooks)
                {
                    if (preHooks.Length > 0)
                    {
                        for (int i = 0; i < preHooks.Length; i++)
                        {
                            await preHooks[i].OnExecutingAsync(emptyRequest, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (hooks.Length > 0)
                    {
                        for (int i = 0; i < hooks.Length; i++)
                        {
                            await hooks[i].OnExecutingAsync(emptyRequest, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                var response = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

                if (_options.EnableRequestHooks)
                {
                    if (postHooks.Length > 0)
                    {
                        for (int i = 0; i < postHooks.Length; i++)
                        {
                            await postHooks[i].OnExecutedAsync(emptyRequest, response, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (hooks.Length > 0)
                    {
                        for (int i = 0; i < hooks.Length; i++)
                        {
                            await hooks[i].OnExecutedAsync(emptyRequest, response, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                return response;
            };

            if (behaviors.Length > 0)
            {
                for (int i = behaviors.Length - 1; i >= 0; i--)
                {
                    var next = handlerDelegate;
                    var behavior = behaviors[i];
                    handlerDelegate = () => behavior.Handle(emptyRequest, cancellationToken, next);
                }
            }

            return handlerDelegate();
        }

        private object GetHandler(Type type)
        {
            var factory = _factoryCache.GetOrAdd(type, CreateFactory);

            return factory(_serviceProvider);
        }

        private IPipelineBehavior<TRequest, TResponse>[] GetBehaviors<TRequest, TResponse>()
        {
            var cacheKey = typeof(IPipelineBehavior<TRequest, TResponse>);
            var factory = _behaviorsFactoryCache.GetOrAdd(cacheKey, CreateCollectionFactory<IPipelineBehavior<TRequest, TResponse>>);
            return (IPipelineBehavior<TRequest, TResponse>[])factory(_serviceProvider);
        }

        private IRequestHook<TRequest, TResponse>[] GetHooks<TRequest, TResponse>()
        {
            var cacheKey = typeof(IRequestHook<TRequest, TResponse>);
            var factory = _hooksFactoryCache.GetOrAdd(cacheKey, CreateCollectionFactory<IRequestHook<TRequest, TResponse>>);
            return (IRequestHook<TRequest, TResponse>[])factory(_serviceProvider);
        }

        private IRequestPreHook<TRequest>[] GetPreHooks<TRequest>()
        {
            var cacheKey = typeof(IRequestPreHook<TRequest>);
            var factory = _preHooksFactoryCache.GetOrAdd(cacheKey, CreateCollectionFactory<IRequestPreHook<TRequest>>);
            return (IRequestPreHook<TRequest>[])factory(_serviceProvider);
        }

        private IRequestPostHook<TRequest, TResponse>[] GetPostHooks<TRequest, TResponse>()
        {
            var cacheKey = typeof(IRequestPostHook<TRequest, TResponse>);
            var factory = _postHooksFactoryCache.GetOrAdd(cacheKey, CreateCollectionFactory<IRequestPostHook<TRequest, TResponse>>);
            return (IRequestPostHook<TRequest, TResponse>[])factory(_serviceProvider);
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

        private static Func<IServiceProvider, Array> CreateCollectionFactory<T>(Type type)
        {
            var providerParam = Expression.Parameter(typeof(IServiceProvider), "provider");

            var getServicesCall = Expression.Call(
                typeof(ServiceProviderServiceExtensions),
                nameof(ServiceProviderServiceExtensions.GetServices),
                new[] { type },
                providerParam
            );

            var toArrayCall = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.ToArray),
                new[] { type },
                getServicesCall
            );
            var castResult = Expression.Convert(toArrayCall, typeof(Array));

            var lambda = Expression.Lambda<Func<IServiceProvider, Array>>(castResult, providerParam);

            return lambda.Compile();
        }
    }

}

