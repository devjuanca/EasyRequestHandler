using System;

namespace EasyRequestHandlers.Events
{
    /// <summary>
    /// Options to control how an event is published.
    /// </summary>
    public class EventPublishOptions
    {
        /// <summary>
        /// Determines if the handlers should run simultaneously.
        /// Default: <c>false</c> (sequential execution).
        /// </summary>
        public bool UseParallelExecution { get; set; }

        /// <summary>
        /// When <c>true</c>, dispatches handlers in the background and returns immediately.
        /// Default: <c>false</c>.
        /// </summary>
        public bool FireAndForget { get; set; }

        /// <summary>
        /// When <c>true</c> and executing sequentially, stops execution at the first handler failure.
        /// Ignored when <see cref="UseParallelExecution"/> is <c>true</c>.
        /// Default: <c>false</c>.
        /// </summary>
        public bool StopOnError { get; set; }

        /// <summary>
        /// Maximum time allowed for background handlers when <see cref="FireAndForget"/> is <c>true</c>.
        /// When <c>null</c> (default), no timeout is applied.
        /// Ignored when <see cref="FireAndForget"/> is <c>false</c>.
        /// Must be a positive value when set.
        /// </summary>
        public TimeSpan? FireAndForgetTimeout { get; set; }
    }
}
