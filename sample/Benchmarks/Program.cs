using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Benchmarks.Benchmarks;

var config = DefaultConfig.Instance.AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByCategory);

BenchmarkRunner.Run<RequestHandlerBenchmarks>(config);

Console.ReadLine();