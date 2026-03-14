using System;

namespace EasyRequestHandlers.Common
{
    /// <summary>
    /// Specifies the execution order for an event handler.
    /// Handlers with lower order values execute first.
    /// Handlers without this attribute execute in registration order after all ordered handlers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HandlerOrderAttribute : Attribute
    {
        public int Order { get; }

        public HandlerOrderAttribute(int order)
        {
            Order = order;
        }
    }
}
