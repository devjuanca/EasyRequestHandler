using EasyRequestHandlers.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;
using System.Reflection;

namespace EasyRequestHandlers.Events
{

    /// <summary>
    /// Provides extension methods to facilitate the registration of event handlers in the dependency injection container.
    /// </summary>
    public static class EventsRegister
    {
        /// <summary>
        /// Registers event handler implementations from specified assemblies into the dependency injection container with the desired service lifetime.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to which the event handlers will be added.</param>
        /// <param name="assemblyTypes">An array of types used to identify the assemblies containing event handlers.</param>
        /// <returns>The modified <see cref="IServiceCollection"/> containing the registered event handlers.</returns>
        public static IServiceCollection AddEasyEventHandlers(this IServiceCollection services, params Type[] assemblyTypes)
        {

            foreach (var type in assemblyTypes)
            {
                var assembly = type.Assembly;

                foreach (var handler in assembly.DefinedTypes)
                {
                    if (handler.IsInterface || handler.IsAbstract)
                    {
                        continue;
                    }

                    var eventInterface = handler.GetInterfaces()
                        .FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IEventHandler<>));

                    if (eventInterface == null)
                    {
                        continue;
                    }

                    var lifetime = handler.GetCustomAttribute<HandlerLifetimeAttribute>()?.Lifetime
                                   ?? ServiceLifetime.Transient;

                    services.TryAddEnumerable(new ServiceDescriptor(eventInterface, handler, lifetime));
                }
            }

            services.TryAddSingleton<IEventPublisher, EventPublisher>();

            return services;
        }
    }
}
