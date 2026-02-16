using System;
using System.Collections.Generic;
using System.Text;

namespace EasyRequestHandlers.Request
{
    /// <summary>
    /// Represents an empty request used for handlers that don't require input parameters.
    /// Uses a singleton pattern to avoid unnecessary allocations.
    /// </summary>
    public sealed class EmptyRequest
    {
        /// <summary>
        /// Gets the singleton instance of EmptyRequest to avoid repeated allocations.
        /// </summary>
        public static EmptyRequest Instance { get; } = new EmptyRequest();

        // Private constructor to enforce singleton pattern
        private EmptyRequest() { }
    }
}
