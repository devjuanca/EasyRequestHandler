using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyRequestHandlers.Events
{
    /// <summary>
    /// Provides methods for publishing events to registered handlers.
    /// </summary>
    public interface IEventPublisher
    {

        /// <summary>
        /// Publishes an event to all registered handlers asynchronously.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event being published.</typeparam>
        /// <param name="event">The event instance to be published.</param>
        /// <param name="useParallelExecution">Determines if the handlers should run simultaneously. Default: `false`</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task PublishAsync<TEvent>(TEvent @event, bool useParallelExecution = false, CancellationToken cancellationToken = default) where TEvent : class;
    }

    /// <summary>
    /// Default implementation of the <see cref="IEventPublisher"/> interface.
    /// Handles the publishing of events by resolving handlers from the service provider and invoking them.
    /// </summary>

    internal sealed class EventPublisher : IEventPublisher
    {
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly ILogger<EventPublisher> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventPublisher"/> class.
        /// </summary>
        /// <param name="scopeFactory">The factory used to create service scopes.</param>
        /// <param name="logger">The logger instance used to log messages.</param>
        public EventPublisher(IServiceScopeFactory scopeFactory, ILogger<EventPublisher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Publishes an event to all registered handlers asynchronously.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event being published.</typeparam>
        /// <param name="event">The event instance to be published.</param>
        /// <param name="useParallelExecution">Determines if the handlers should run simultaneously. Default: `false`</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when event is null.</exception>
        public async Task PublishAsync<TEvent>(TEvent @event, bool useParallelExecution = false, CancellationToken cancellationToken = default) where TEvent : class
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            using var scope = _scopeFactory.CreateScope();

            var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>().ToArray();

            if (handlers.Length == 0)
            {
                _logger.LogDebug("No handlers registered for event type {EventType}", typeof(TEvent).Name);
                return;
            }
            
            _logger.LogDebug("Publishing event {EventType} to {HandlerCount} handler(s)", typeof(TEvent).Name, handlers.Length);
            
            await HandleEventsAsync(@event, handlers, useParallelExecution, cancellationToken);
            
        }

        private async Task HandleEventsAsync<TEvent>(TEvent @event, IReadOnlyList<IEventHandler<TEvent>> handlers, bool useParallelExecution, CancellationToken cancellationToken = default) where TEvent : class
        {
            if (useParallelExecution)
            {
                // Create a mapping of tasks to handlers for efficient lookup on failure
                var taskHandlerPairs = handlers.Select(h => new
                {
                    Task = h.HandleAsync(@event, cancellationToken),
                    Handler = h
                }).ToList();

                try
                {
                    await Task.WhenAll(taskHandlerPairs.Select(p => p.Task)).ConfigureAwait(false);
                }
                catch
                {
                    // Log each failed handler with its specific error
                    foreach (var pair in taskHandlerPairs.Where(p => p.Task.IsFaulted))
                    {
                        foreach (var ex in pair.Task.Exception!.InnerExceptions)
                        {
                            _logger.LogError(ex, "Error in parallel event handler {HandlerType} for event {EventType}", 
                                pair.Handler.GetType().Name, typeof(TEvent).Name);
                        }
                    }

                    throw;
                }
            }
            else
            {
                // Sequential execution - fail fast on first error
                foreach (var handler in handlers)
                {
                    try
                    {
                        await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing event handler {HandlerType} for event {EventType}", 
                            handler.GetType().Name, typeof(TEvent).Name);
                        throw;
                    }
                }
            }
        }
    }
}