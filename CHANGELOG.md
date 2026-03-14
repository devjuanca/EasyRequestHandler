# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-03-14

### Added
- **Fire-and-forget event publishing**: `PublishAsync` now accepts a `fireAndForget` parameter (default `true`). When enabled, handlers are dispatched in the background and the method returns immediately. Errors in fire-and-forget mode are logged but do not propagate to the caller.
- **Handler ordering**: New `[HandlerOrder(int)]` attribute to control event handler execution priority. Lower values run first. Handlers without the attribute preserve their registration order.
- **`HandlerOrderAttribute`**: New attribute in `EasyRequestHandlers.Common` for event handler ordering.
- 18 new tests covering fire-and-forget, handler ordering, error resilience, registration validation, behavior execution order, hook auto-discovery, and handler lifetime.

### Changed
- **Event error resilience**: A failing event handler no longer stops other handlers from executing. In both sequential and parallel modes, all handlers run to completion. Errors are logged individually and collected into an `AggregateException` thrown after all handlers finish (when `fireAndForget` is `false`).
- **Registration validation**: Calling `.WithBehaviors()` or `.WithBehavior()` without `.WithMediatorPattern()` now throws `InvalidOperationException` instead of silently doing nothing.
- **`RequestHandlerBuilder` constructor** changed from `public` to `internal` to prevent direct instantiation outside the fluent API.
- **`WithBehavior(Type)`** now delegates to `WithBehaviors(params Type[])`, removing duplicated validation logic.
- **`GetHandlerKey`** now detects duplicate no-input handlers (`RequestHandler<TResponse>`) in addition to input handlers, providing consistent duplicate detection.
- **Event handler registration** uses `TryAddEnumerable` to prevent duplicate registrations when the same assembly marker is passed multiple times. `IEventPublisher` uses `TryAddSingleton`.

### Performance
- **Request pipeline**: Replaced `List<T>` with `T[]` (`.ToArray()`) for all service collections resolved per request — lower memory overhead and faster indexed access.
- **Behavior pipeline**: Eliminated `.Reverse()` allocation in `ExecuteWithBehaviorsOnly` — replaced with zero-allocation backward `for` loop.
- **Service enumeration**: Materialized `GetServices()` to array before `.Any()` check, eliminating double-enumeration of service collections.
- **Event publisher**: Replaced anonymous types with `ValueTuple` arrays in parallel dispatch — avoids heap allocation per handler.
- **Event publisher**: Cached `HandlerOrderAttribute` reflection lookups in a static `ConcurrentDictionary` — reflection happens once per handler type.
- **Event publisher**: `List<Exception>` for error collection is now lazily allocated only when the first error occurs.
- **Event publisher**: Eliminated double array allocation — handlers are sorted in-place using `Array.Sort` with a custom comparer instead of LINQ `.OrderBy().ToArray()`.
- **Handler registration**: `assembly.GetTypes()` is now called once per assembly instead of four times when hooks are enabled.
- **Handler registration**: Replaced three separate type-scanning loops for hooks with a single pass over assembly types.
- **Event handler registration**: Single-pass discovery with direct type comparison instead of string-based interface matching (`b.Name == "IEventHandler\`1"`).
- **Dead code**: Removed unused `_emptyArray` static field from `Sender`.

### Documentation
- README rewritten with corrected API names (`IPipelineBehavior`, `.WithBehaviors()`), documented all new features (fire-and-forget, handler ordering, handler lifetime, `Empty` type, error resilience), added execution mode table, pipeline diagrams, and full registration example.

### Breaking Changes
- `IEventPublisher.PublishAsync` signature changed: new `bool fireAndForget = true` parameter added between `useParallelExecution` and `cancellationToken`. Callers passing `cancellationToken` positionally as the third argument must switch to a named parameter: `cancellationToken: ct`.
- `RequestHandlerBuilder` constructor is now `internal`. Use `services.AddEasyRequestHandlers()` instead of `new RequestHandlerBuilder()`.
- Calling `.WithBehaviors()` without `.WithMediatorPattern()` now throws instead of silently returning.
- Sequential event execution no longer stops on first handler error — all handlers now execute regardless of failures.

## [1.1.5] - 2026-02-16

### Added
- 2-parameter constructor overload for Sender to maintain binary compatibility

### Changed
- Made EmptyRequest.Instance a public property (was internal) for consumer access
- Enhanced README with comprehensive features, benefits, quick start guide, and use cases

### Fixed
- Cancellation exception filtering - OperationCanceledException no longer logged as errors
- Duplicate error logging removed from pipeline (consolidated to single layer)
- Binary compatibility maintained with constructor overload pattern

### Documentation
- README significantly enhanced with feature comparisons, quick start, and use case examples

## [1.1.4] - 2026-02-15

### Added
- Comprehensive error handling in Sender pipeline with detailed logging
- Optional ILogger<Sender> support for request processing observability
- Input validation to EventPublisher with ArgumentNullException for null events
- 6 new error handling test cases covering edge scenarios
- Extensive XML documentation for ISender, IRequestHook interfaces, and RequestHandlerOptions
- Debug logging for event publishing with handler count information

### Changed
- EmptyRequest now uses singleton pattern to reduce memory allocations
- EventPublisher error messages now include handler and event type information
- Optimized EventPublisher task-handler mapping to avoid O(n) IndexOf lookups
- Enhanced error context in all exception scenarios

### Fixed
- EventPublisher performance issue with IndexOf in error reporting (O(n²) to O(n))
- Missing error context in hook and behavior execution failures

### Security
- CodeQL security analysis: 0 vulnerabilities found
- All changes maintain secure coding practices

### Documentation
- Added IMPROVEMENTS.md with comprehensive review and migration guide
- Enhanced code comments and XML documentation throughout

### Breaking Changes
- None - all changes are backward compatible

## [1.1.3] - Previous Release
- Enhancements in ISender.cs included an empty array cache for memory optimization
- Refactor of the request handling pipeline to improve behavior execution and request hooks

[1.2.0]: https://github.com/devjuanca/EasyRequestHandler/compare/v1.1.5...v1.2.0
[1.1.5]: https://github.com/devjuanca/EasyRequestHandler/compare/v1.1.4...v1.1.5
[1.1.4]: https://github.com/devjuanca/EasyRequestHandler/compare/v1.1.3...v1.1.4
[1.1.3]: https://github.com/devjuanca/EasyRequestHandler/releases/tag/v1.1.3
