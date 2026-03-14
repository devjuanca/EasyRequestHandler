using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Benchmarks.Scenarios.ComplexRequest;
using Benchmarks.Scenarios.SimpleRequest;
using EasyRequestHandlers.Request;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ISender = EasyRequestHandlers.Request.ISender;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 5, iterationCount: 20)]

public class RequestHandlerBenchmarks
{
    private IServiceProvider _easyRequestServiceProvider = null!;
    private IServiceProvider _mediatRServiceProvider = null!;
    private IServiceProvider _easyDirectServiceProvider = null!;
    private MediatRComplexRequest _mediatRComplexRequest = null!;
    private EasyComplexRequest _easyComplexRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup EasyRequestHandler
        var easyRequestServices = new ServiceCollection();

        // Add logging
        easyRequestServices.AddLogging(_ => { });

        easyRequestServices.AddEasyRequestHandlers(typeof(RequestHandlerBenchmarks))
            .WithMediatorPattern()
            //.WithBehavior(typeof(EasyLoggingBehavior<,>))
            .Build();

        _easyRequestServiceProvider = easyRequestServices.BuildServiceProvider();

        // Setup MediatR
        var mediatRServices = new ServiceCollection();

        // Add logging
        mediatRServices.AddLogging(_ => { });

        mediatRServices.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RequestHandlerBenchmarks>();
            //cfg.AddOpenBehavior(typeof(MediatRLoggingBehavior<,>));
        });

        _mediatRServiceProvider = mediatRServices.BuildServiceProvider();

        // Setup EasyRequestHandler with direct injection (no mediator)
        var easyDirectServices = new ServiceCollection();
        // Add logging
        easyDirectServices.AddLogging(_ => { });

        easyDirectServices.AddEasyRequestHandlers(typeof(RequestHandlerBenchmarks))
            // Not calling WithMediatorPattern() to use direct handler injection
            .Build();

        _easyDirectServiceProvider = easyDirectServices.BuildServiceProvider();

        // Initialize complex requests
        _mediatRComplexRequest = new MediatRComplexRequest
        {
            Data = "Sample data for processing",
            Metadata = new Dictionary<string, string>
            {
                { "tag1", "value1" },
                { "tag2", "value2" },
                { "tag3", "value3" }
            }
        };

        _easyComplexRequest = new EasyComplexRequest
        {
            Data = "Sample data for processing",
            Metadata = new Dictionary<string, string>
            {
                { "tag1", "value1" },
                { "tag2", "value2" },
                { "tag3", "value3" }
            }
        };
    }

    // --- Single simple request ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Simple")]
    public async Task<SimpleResponse> Simple_MediatR_Mediator()
    {
        using var scope = _mediatRServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new MediatRSimpleRequest { Value = 42 });
    }

    [Benchmark]
    [BenchmarkCategory("Simple")]
    public async Task<SimpleResponse> Simple_Easy_Mediator()
    {
        using var scope = _easyRequestServiceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.SendAsync<EasySimpleRequest, SimpleResponse>(new EasySimpleRequest { Value = 42 });
    }

    [Benchmark]
    [BenchmarkCategory("Simple")]
    public async Task<SimpleResponse> Simple_Easy_Direct()
    {
        using var scope = _easyDirectServiceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<EasySimpleRequestHandler>();
        return await handler.HandleAsync(new EasySimpleRequest { Value = 42 });
    }

    // --- Single complex request (with logger injection + string processing) ---

    [Benchmark]
    [BenchmarkCategory("Complex")]
    public async Task<ComplexResponse> Complex_MediatR_Mediator()
    {
        using var scope = _mediatRServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(_mediatRComplexRequest);
    }

    [Benchmark]
    [BenchmarkCategory("Complex")]
    public async Task<ComplexResponse> Complex_Easy_Mediator()
    {
        using var scope = _easyRequestServiceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.SendAsync<EasyComplexRequest, ComplexResponse>(_easyComplexRequest);
    }

    [Benchmark]
    [BenchmarkCategory("Complex")]
    public async Task<ComplexResponse> Complex_Easy_Direct()
    {
        using var scope = _easyDirectServiceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<EasyComplexRequestHandler>();
        return await handler.HandleAsync(_easyComplexRequest);
    }

    // --- 100 concurrent simple requests ---

    [Benchmark]
    [BenchmarkCategory("Concurrent_Simple")]
    public async Task Concurrent100_Simple_MediatR_Mediator()
    {
        var tasks = new List<Task<SimpleResponse>>();

        for (int i = 0; i < 100; i++)
        {
            var scope = _mediatRServiceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            tasks.Add(mediator.Send(new MediatRSimpleRequest { Value = i }).ContinueWith(t =>
            {
                scope.Dispose();
                return t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [BenchmarkCategory("Concurrent_Simple")]
    public async Task Concurrent100_Simple_Easy_Mediator()
    {
        var tasks = new List<Task<SimpleResponse>>();

        for (int i = 0; i < 100; i++)
        {
            var scope = _easyRequestServiceProvider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            tasks.Add(sender.SendAsync<EasySimpleRequest, SimpleResponse>(new EasySimpleRequest { Value = i }).ContinueWith(t =>
            {
                scope.Dispose();
                return t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [BenchmarkCategory("Concurrent_Simple")]
    public async Task Concurrent100_Simple_Easy_Direct()
    {
        var tasks = new List<Task<SimpleResponse>>();

        for (int i = 0; i < 100; i++)
        {
            var scope = _easyDirectServiceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<EasySimpleRequestHandler>();
            tasks.Add(handler.HandleAsync(new EasySimpleRequest { Value = i }).ContinueWith(t =>
            {
                scope.Dispose();
                return t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously));
        }
        await Task.WhenAll(tasks);
    }

    // --- 50 concurrent complex requests ---

    [Benchmark]
    [BenchmarkCategory("Concurrent_Complex")]
    public async Task Concurrent50_Complex_MediatR_Mediator()
    {
        var tasks = new List<Task<ComplexResponse>>();

        for (int i = 0; i < 50; i++)
        {
            var scope = _mediatRServiceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var request = new MediatRComplexRequest
            {
                Data = $"Data {i}",
                Metadata = new Dictionary<string, string>
                {
                    { $"key{i}_1", $"value{i}_1" },
                    { $"key{i}_2", $"value{i}_2" }
                }
            };
            tasks.Add(mediator.Send(request).ContinueWith(t =>
            {
                scope.Dispose();
                return t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [BenchmarkCategory("Concurrent_Complex")]
    public async Task Concurrent50_Complex_Easy_Mediator()
    {
        var tasks = new List<Task<ComplexResponse>>();

        for (int i = 0; i < 50; i++)
        {
            var scope = _easyRequestServiceProvider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var request = new EasyComplexRequest
            {
                Data = $"Data {i}",
                Metadata = new Dictionary<string, string>
                {
                    { $"key{i}_1", $"value{i}_1" },
                    { $"key{i}_2", $"value{i}_2" }
                }
            };
            tasks.Add(sender.SendAsync<EasyComplexRequest, ComplexResponse>(request).ContinueWith(t =>
            {
                scope.Dispose();
                return t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [BenchmarkCategory("Concurrent_Complex")]
    public async Task Concurrent50_Complex_Easy_Direct()
    {
        var tasks = new List<Task<ComplexResponse>>();

        for (int i = 0; i < 50; i++)
        {
            var scope = _easyDirectServiceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<EasyComplexRequestHandler>();
            var request = new EasyComplexRequest
            {
                Data = $"Data {i}",
                Metadata = new Dictionary<string, string>
                {
                    { $"key{i}_1", $"value{i}_1" },
                    { $"key{i}_2", $"value{i}_2" }
                }
            };
            tasks.Add(handler.HandleAsync(request).ContinueWith(t =>
            {
                scope.Dispose();
                return t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously));
        }
        await Task.WhenAll(tasks);
    }
}
