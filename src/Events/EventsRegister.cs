using EasyRequestHandlers.Common;
using Microsoft.Extensions.DependencyInjection;
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

                var eventsHandlers = assembly.DefinedTypes.Where(a => a.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)) && !a.IsInterface && !a.IsAbstract).ToList();

                foreach (var handler in eventsHandlers)
                {
                    var eventInterface = handler.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)) ?? throw new Exception("Events handlers must implement IEventHandler<TEvent>");

                    var lifetimeAttribute = handler.GetCustomAttribute<HandlerLifetimeAttribute>();

                    if (lifetimeAttribute == null)
                    {
                        services.AddTransient(eventInterface, handler);
                        continue;
                    }
                    switch (lifetimeAttribute.Lifetime)
                    {
                        case ServiceLifetime.Singleton:
                            services.AddSingleton(eventInterface, handler);
                            break;
                        case ServiceLifetime.Scoped:
                            services.AddScoped(eventInterface, handler);
                            break;
                        case ServiceLifetime.Transient:
                            services.AddTransient(eventInterface, handler);
                            break;
                    }
                }
            }

            services.AddSingleton<IEventPublisher, EventPublisher>();

            return services;
        }
    }
}