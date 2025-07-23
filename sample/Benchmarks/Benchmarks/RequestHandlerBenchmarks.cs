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
[SimpleJob(warmupCount: 3, iterationCount: 10)]

public class RequestHandlerBenchmarks
{
    private IServiceProvider _easyRequestServiceProvider = null!;
    private IServiceProvider _mediatRServiceProvider = null!;
    private IServiceProvider _easyDirectServiceProvider = null!;
    private ISender _easyRequestSender = null!;
    private IMediator _mediator = null!;
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

        _easyRequestSender = _easyRequestServiceProvider.GetRequiredService<ISender>();

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

        _mediator = _mediatRServiceProvider.GetRequiredService<IMediator>();

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

    // Simple request benchmarks (existing)
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SimpleRequest")]
    public async Task<SimpleResponse> MediatR_SimpleRequest()
    {
        return await _mediator.Send(new MediatRSimpleRequest { Value = 42 });
    }

    [Benchmark]
    [BenchmarkCategory("SimpleRequest")]
    public async Task<SimpleResponse> EasyRequestHandler_SimpleRequest()
    {
        return await _easyRequestSender.SendAsync<EasySimpleRequest, SimpleResponse>(new EasySimpleRequest { Value = 42 });
    }

    // Direct handler injection
    [Benchmark]
    [BenchmarkCategory("SimpleRequest")]
    public async Task<SimpleResponse> EasyRequestHandler_DirectInjection()
    {
        var handler = _easyDirectServiceProvider.GetRequiredService<EasySimpleRequestHandler>();
        return await handler.HandleAsync(new EasySimpleRequest { Value = 42 });
    }

    // Complex request benchmarks (new)
    [Benchmark]
    [BenchmarkCategory("ComplexRequest")]
    public async Task<ComplexResponse> MediatR_ComplexRequest()
    {
        return await _mediator.Send(_mediatRComplexRequest);
    }

    [Benchmark]
    [BenchmarkCategory("ComplexRequest")]
    public async Task<ComplexResponse> EasyRequestHandler_ComplexRequest()
    {
        return await _easyRequestSender.SendAsync<EasyComplexRequest, ComplexResponse>(_easyComplexRequest);
    }

    // Complex direct handler injection
    [Benchmark]
    [BenchmarkCategory("ComplexRequest")]
    public async Task<ComplexResponse> EasyRequestHandler_ComplexDirectInjection()
    {
        var handler = _easyDirectServiceProvider.GetRequiredService<EasyComplexRequestHandler>();
        return await handler.HandleAsync(_easyComplexRequest);
    }

    // Load test benchmarks (existing)
    [Benchmark]
    [BenchmarkCategory("LoadTest")]
    public async Task MediatR_LoadTest()
    {
        var tasks = new List<Task<SimpleResponse>>();

        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_mediator.Send(new MediatRSimpleRequest { Value = i }));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [BenchmarkCategory("LoadTest")]
    public async Task EasyRequestHandler_LoadTest()
    {
        var tasks = new List<Task<SimpleResponse>>();

        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_easyRequestSender.SendAsync<EasySimpleRequest, SimpleResponse>(new EasySimpleRequest { Value = i }));
        }
        await Task.WhenAll(tasks);
    }

    // Direct handler injection load test
    [Benchmark]
    [BenchmarkCategory("LoadTest")]
    public async Task EasyRequestHandler_DirectLoadTest()
    {
        var tasks = new List<Task<SimpleResponse>>();

        for (int i = 0; i < 100; i++)
        {
            var handler = _easyDirectServiceProvider.GetRequiredService<EasySimpleRequestHandler>();
            tasks.Add(handler.HandleAsync(new EasySimpleRequest { Value = i }));
        }
        await Task.WhenAll(tasks);
    }

    // Complex load test benchmarks (new)
    [Benchmark]
    [BenchmarkCategory("ComplexLoadTest")]
    public async Task MediatR_ComplexLoadTest()
    {
        var tasks = new List<Task<ComplexResponse>>();

        for (int i = 0; i < 50; i++)
        {
            var request = new MediatRComplexRequest
            {
                Data = $"Data {i}",
                Metadata = new Dictionary<string, string>
                {
                    { $"key{i}_1", $"value{i}_1" },
                    { $"key{i}_2", $"value{i}_2" }
                }
            };
            tasks.Add(_mediator.Send(request));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [BenchmarkCategory("ComplexLoadTest")]
    public async Task EasyRequestHandler_ComplexLoadTest()
    {
        var tasks = new List<Task<ComplexResponse>>();

        for (int i = 0; i < 50; i++)
        {
            var request = new EasyComplexRequest
            {
                Data = $"Data {i}",
                Metadata = new Dictionary<string, string>
                {
                    { $"key{i}_1", $"value{i}_1" },
                    { $"key{i}_2", $"value{i}_2" }
                }
            };
            tasks.Add(_easyRequestSender.SendAsync<EasyComplexRequest, ComplexResponse>(request));
        }
        await Task.WhenAll(tasks);
    }

    // Complex direct handler injection load test
    [Benchmark]
    [BenchmarkCategory("ComplexLoadTest")]
    public async Task EasyRequestHandler_ComplexDirectLoadTest()
    {
        var tasks = new List<Task<ComplexResponse>>();

        for (int i = 0; i < 50; i++)
        {
            var handler = _easyDirectServiceProvider.GetRequiredService<EasyComplexRequestHandler>();
            var request = new EasyComplexRequest
            {
                Data = $"Data {i}",
                Metadata = new Dictionary<string, string>
                {
                    { $"key{i}_1", $"value{i}_1" },
                    { $"key{i}_2", $"value{i}_2" }
                }
            };
            tasks.Add(handler.HandleAsync(request));
        }
        await Task.WhenAll(tasks);
    }
}