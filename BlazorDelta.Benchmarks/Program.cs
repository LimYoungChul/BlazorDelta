
using BenchmarkDotNet.Running;
using BlazorDelta.Benchmarks;

var summary = BenchmarkRunner.Run<ComponentBenchmark>();
