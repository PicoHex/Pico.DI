using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Pico.DI.Benchmarks;

// Run all benchmarks
var config = DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
