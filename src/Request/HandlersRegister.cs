﻿using EasyRequestHandlers.Common;
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

                var foundHandlers = assembly.GetTypes().Where(x => typeof(BaseHandler)
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
                    var hookTypes = assembly.GetTypes().Where(x => !x.IsAbstract &&
                            !x.IsInterface &&
                            x.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHook<,>))).ToList();

                    var preHookTypes = assembly.GetTypes().Where(x => !x.IsAbstract &&
                            !x.IsInterface &&
                            x.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestPreHook<>))).ToList();

                    var postHookTypes = assembly.GetTypes().Where(x => !x.IsAbstract && 
                            !x.IsInterface &&
                            x.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestPostHook<,>))).ToList();

                    foreach (var hookType in hookTypes)
                    {
                        var interfaces = hookType.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHook<,>));

                        foreach (var hookInterface in interfaces)
                        {
                            services.TryAdd(new ServiceDescriptor(hookInterface, hookType, ServiceLifetime.Transient));
                        }
                    }

                    foreach (var preHookType in preHookTypes)
                    {
                        var interfaces = preHookType.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestPreHook<>));
                        foreach (var preHookInterface in interfaces)
                        {
                            services.TryAdd(new ServiceDescriptor(preHookInterface, preHookType, ServiceLifetime.Transient));
                        }
                    }

                    foreach (var postHookType in postHookTypes)
                    {
                        var interfaces = postHookType.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestPostHook<,>));
                        foreach (var postHookInterface in interfaces)
                        {
                            services.TryAdd(new ServiceDescriptor(postHookInterface, postHookType, ServiceLifetime.Transient));
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
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(RequestHandler<,>))
                {
                    var genericArgs = baseType.GetGenericArguments();

                    return $"{baseType.Name}<{genericArgs[0].FullName},{genericArgs[1].FullName}>";
                }

                baseType = baseType.BaseType;
            }
            return type.FullName;
        }

    }
}
