namespace EasyRequestHandlers.Request
{
    /// <summary>
    /// Configuration options for request handlers registration and behavior.
    /// </summary>
    public class RequestHandlerOptions
    {
        /// <summary>
        /// Gets or sets whether the mediator pattern is enabled for request handling.
        /// When enabled, ISender can be injected to send requests to handlers.
        /// </summary>
        internal bool EnableMediatorPattern { get; set; }

        /// <summary>
        /// Gets or sets whether request hooks (pre/post execution hooks) are enabled.
        /// </summary>
        internal bool EnableRequestHooks { get; set; }

        /// <summary>
        /// Gets or sets whether handler direct injection is enabled.
        /// When enabled, handler classes can be injected directly.
        /// </summary>
        internal bool EnableHandlerInjection { get; set; } = true;

        /// <summary>
        /// Gets or sets whether any pipeline behaviors have been registered.
        /// </summary>
        internal bool HasBehaviors { get; set; }
    }
}
