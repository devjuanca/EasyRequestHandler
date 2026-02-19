# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-02-19

### Added
- `VoidRequestHandler<TRequest>` base class for command-style handlers that don't return a response
- `ISender.SendAsync<TRequest>(request)` overload for fire-and-forget commands without requiring `Empty` as response type
- 5 new tests for void handler registration and usage

### Changed
- **Performance**: Behaviors are now materialized to a list before checking count, avoiding double enumeration of `IEnumerable<IPipelineBehavior>`
- **Performance**: Reverse iteration of behaviors uses indexed loop instead of LINQ `.Reverse()`, avoiding an extra collection allocation
- **Performance**: `HandlersRegister` now calls `assembly.GetTypes()` once per assembly instead of up to 4 times
- **Performance**: `EventsRegister` uses type-safe generic type comparison (`typeof(IEventHandler<>)`) instead of fragile string matching (`"IEventHandler\`1"`)
- Handler key generation updated to support `VoidRequestHandler<TRequest>` duplicate detection

### Breaking Changes
- None - all changes are backward compatible

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
