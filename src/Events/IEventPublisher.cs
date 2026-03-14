using EasyRequestHandlers.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        /// Publishes an event to all registered handlers asynchronously using default options (sequential, awaited).
        /// </summary>
        /// <typeparam name="TEvent">The type of the event being published.</typeparam>
        /// <param name="event">The event instance to be published.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class;

        /// <summary>
        /// Publishes an event to all registered handlers asynchronously with the specified options.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event being published.</typeparam>
        /// <param name="event">The event instance to be published.</param>
        /// <param name="options">Options to control execution mode, error handling, and timeouts.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : class;
    }

    /// <summary>
    /// Default implementation of the <see cref="IEventPublisher"/> interface.
    /// Handles the publishing of events by resolving handlers from the service provider and invoking them.
    /// </summary>

    internal sealed class EventPublisher : IEventPublisher
    {
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly ILogger<EventPublisher> _logger;

        private static readonly ConcurrentDictionary<Type, int> _orderCache = new ConcurrentDictionary<Type, int>();

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

        /// <inheritdoc />
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            return PublishCoreAsync(@event, false, false, cancellationToken);
        }

        /// <inheritdoc />
        public Task PublishAsync<TEvent>(TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : class
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.FireAndForgetTimeout.HasValue && options.FireAndForgetTimeout.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "FireAndForgetTimeout must be a positive value.");
            }

            if (options.FireAndForget)
            {
                _ = PublishInBackgroundAsync(@event, options.UseParallelExecution, options.StopOnError, options.FireAndForgetTimeout);

                return Task.CompletedTask;
            }

            return PublishCoreAsync(@event, options.UseParallelExecution, options.StopOnError, cancellationToken);
        }

        private async Task PublishInBackgroundAsync<TEvent>(TEvent @event, bool useParallelExecution, bool stopOnError, TimeSpan? timeout) where TEvent : class
        {
            CancellationTokenSource cts = null;

            try
            {
                CancellationToken token;

                if (timeout.HasValue)
                {
                    cts = new CancellationTokenSource(timeout.Value);
                    token = cts.Token;
                }
                else
                {
                    token = CancellationToken.None;
                }

                await PublishCoreAsync(@event, useParallelExecution, stopOnError, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts != null && cts.IsCancellationRequested)
            {
                _logger.LogWarning("Fire-and-forget event publishing for {EventType} was cancelled due to timeout ({Timeout})", typeof(TEvent).Name, timeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during fire-and-forget event publishing for {EventType}", typeof(TEvent).Name);
            }
            finally
            {
                cts?.Dispose();
            }
        }

        private async Task PublishCoreAsync<TEvent>(TEvent @event, bool useParallelExecution, bool stopOnError, CancellationToken cancellationToken) where TEvent : class
        {
            using var scope = _scopeFactory.CreateScope();

            var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>().ToArray();

            if (handlers.Length == 0)
            {
                _logger.LogDebug("No handlers registered for event type {EventType}", typeof(TEvent).Name);
                return;
            }

            // Sort in-place by cached HandlerOrderAttribute. Array.Sort is unstable,
            // so we use the original index as a tiebreaker to preserve registration order.
            if (handlers.Length > 1)
            {
                var keys = new (int Order, int Index)[handlers.Length];

                for (int i = 0; i < handlers.Length; i++)
                {
                    keys[i] = (GetHandlerOrder(handlers[i].GetType()), i);
                }

                Array.Sort(keys, handlers, HandlerOrderComparer.Instance);
            }

            _logger.LogDebug("Publishing event {EventType} to {HandlerCount} handler(s)", typeof(TEvent).Name, handlers.Length);

            List<Exception> errors = null;

            if (useParallelExecution)
            {
                var taskHandlerPairs = new (Task Task, IEventHandler<TEvent> Handler)[handlers.Length];

                for (int i = 0; i < handlers.Length; i++)
                {
                    Task task;
                    try
                    {
                        task = handlers[i].HandleAsync(@event, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        task = Task.FromException(ex);
                    }
                    taskHandlerPairs[i] = (task, handlers[i]);
                }

                var tasks = new Task[taskHandlerPairs.Length];

                for (int i = 0; i < taskHandlerPairs.Length; i++)
                {
                    tasks[i] = taskHandlerPairs[i].Task;
                }

                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch
                {
                    for (int i = 0; i < taskHandlerPairs.Length; i++)
                    {
                        if (taskHandlerPairs[i].Task.IsFaulted)
                        {
                            foreach (var ex in taskHandlerPairs[i].Task.Exception!.InnerExceptions)
                            {
                                _logger.LogError(ex, "Error in parallel event handler {HandlerType} for event {EventType}",
                                    taskHandlerPairs[i].Handler.GetType().Name, typeof(TEvent).Name);

                                (errors ??= new List<Exception>()).Add(
                                    new InvalidOperationException(
                                        $"Event handler '{taskHandlerPairs[i].Handler.GetType().Name}' failed for event '{typeof(TEvent).Name}'.", ex));
                            }
                        }
                    }
                }
            }
            else
            {
                // Sequential execution
                for (int i = 0; i < handlers.Length; i++)
                {
                    try
                    {
                        await handlers[i].HandleAsync(@event, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing event handler {HandlerType} for event {EventType}",
                            handlers[i].GetType().Name, typeof(TEvent).Name);

                        (errors ??= new List<Exception>()).Add(
                            new InvalidOperationException(
                                $"Event handler '{handlers[i].GetType().Name}' failed for event '{typeof(TEvent).Name}'.", ex));

                        if (stopOnError)
                        {
                            break;
                        }
                    }
                }
            }

            if (errors != null)
            {
                throw new AggregateException($"One or more event handlers for {typeof(TEvent).Name} failed.", errors);
            }
        }

        private static int GetHandlerOrder(Type handlerType)
        {
            return _orderCache.GetOrAdd(handlerType, t => t.GetCustomAttribute<HandlerOrderAttribute>()?.Order ?? int.MaxValue);
        }

        private sealed class HandlerOrderComparer : IComparer<(int Order, int Index)>
        {
            public static readonly HandlerOrderComparer Instance = new HandlerOrderComparer();

            public int Compare((int Order, int Index) x, (int Order, int Index) y)
            {
                var orderComparison = x.Order.CompareTo(y.Order);

                return orderComparison != 0 ? orderComparison : x.Index.CompareTo(y.Index);
            }
        }
    }
}
