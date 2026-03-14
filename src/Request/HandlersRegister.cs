using EasyRequestHandlers.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EasyRequestHandlers.Request
{
    /// <summary>
    /// Provides extension methods to register request handlers into the dependency injection container.
    /// </summary>
    public static class HandlersRegister
    {
        /// <summary>
        /// Registers request handlers from the specified assemblies into the dependency injection container with the desired service lifetime.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to which the request handlers will be added.</param>
        /// <param name="assemblyMarkers">An array of types used to identify the assemblies containing request handlers.</param>
        /// <returns>The modified <see cref="IServiceCollection"/> containing the registered request handlers.</returns>
        public static RequestHandlerBuilder AddEasyRequestHandlers(this IServiceCollection services, params Type[] assemblyMarkers)
        {
            var options = new RequestHandlerOptions();

            return new RequestHandlerBuilder(services, options, assemblyMarkers);
        }

        internal static IServiceCollection RegisterHandlers(IServiceCollection services, RequestHandlerOptions options, Type[] assemblyMarkers)
        {
            var handlers = new List<Type>();

            var handlerKeys = new HashSet<string>();

            foreach (var type in assemblyMarkers)
            {
                var assembly = type.Assembly;

                var allTypes = assembly.GetTypes();

                var foundHandlers = allTypes.Where(x => typeof(BaseHandler)
                                                       .IsAssignableFrom(x) &&
                                                        !x.IsAbstract &&
                                                        !x.IsInterface)
                                                       .ToList();

                if (foundHandlers.Count != 0)
                {
                    handlers.AddRange(foundHandlers);
                }

                if (options.EnableRequestHooks)
                {
                    foreach (var t in allTypes)
                    {
                        if (t.IsAbstract || t.IsInterface) continue;

                        foreach (var iface in t.GetInterfaces())
                        {
                            if (!iface.IsGenericType) continue;

                            var def = iface.GetGenericTypeDefinition();

                            if (def == typeof(IRequestHook<,>) || def == typeof(IRequestPreHook<>) || def == typeof(IRequestPostHook<,>))
                            {
                                services.TryAdd(new ServiceDescriptor(iface, t, ServiceLifetime.Transient));
                            }
                        }
                    }
                }

            }

            foreach (var handler in handlers)
            {
                var key = GetHandlerKey(handler);

                if (!handlerKeys.Add(key))
                {
                    throw new InvalidOperationException($"Duplicate handler detected for request signature: {key}");
                }

                var baseType = handler.BaseType;

                if (options.EnableMediatorPattern)
                {
                    if (baseType?.IsGenericType == true)
                    {
                        var genericTypeDef = baseType.GetGenericTypeDefinition();
                        
                        // Register for RequestHandler<TRequest, TResponse>
                        if (genericTypeDef == typeof(RequestHandler<,>))
                        {
                            services.TryAdd(new ServiceDescriptor(baseType, handler, ServiceLifetime.Scoped));
                        }
                        // Register for RequestHandler<TResponse> (no-input handlers)
                        else if (genericTypeDef == typeof(RequestHandler<>))
                        {
                            services.TryAdd(new ServiceDescriptor(baseType, handler, ServiceLifetime.Scoped));
                        }
                    }
                }
                
                services.TryAdd(new ServiceDescriptor(handler, handler, ServiceLifetime.Scoped));             
            }

            if (options.EnableMediatorPattern)
            {
                services.TryAdd(new ServiceDescriptor(typeof(ISender), typeof(Sender), ServiceLifetime.Scoped));
            }

            services.AddSingleton(options);

            return services;
        }

        private static string GetHandlerKey(Type type)
        {
            var baseType = type.BaseType;

            while (baseType != null && baseType != typeof(BaseHandler))
            {
                if (baseType.IsGenericType)
                {
                    var genericTypeDef = baseType.GetGenericTypeDefinition();

                    if (genericTypeDef == typeof(RequestHandler<,>))
                    {
                        var genericArgs = baseType.GetGenericArguments();
                        return $"{baseType.Name}<{genericArgs[0].FullName},{genericArgs[1].FullName}>";
                    }

                    if (genericTypeDef == typeof(RequestHandler<>))
                    {
                        var genericArgs = baseType.GetGenericArguments();
                        return $"{baseType.Name}<{genericArgs[0].FullName}>";
                    }
                }

                baseType = baseType.BaseType;
            }
            return type.FullName;
        }

    }
}
