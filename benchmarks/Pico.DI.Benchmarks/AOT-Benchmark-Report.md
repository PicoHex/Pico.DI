# Pico.DI Benchmarks — AOT & Managed Summary

Date: 2025-12-29

This file summarizes the Native AOT manual benchmark run and points to the BenchmarkDotNet artifacts produced during the managed run.

## AOT (manual Stopwatch) results

Source: `benchmarks/Pico.DI.Benchmarks` — manual Stopwatch loops executed by the AOT-friendly runner.

| Metric | Pico.DI (AOT manual, ns) | MS.DI (AOT manual, ns) | Speedup (Pico.DI vs MS.DI) |
|---|---:|---:|---:|
| Container Setup | 778.82 | 1527.70 | 1.96x |
| Singleton Resolve | 16.38 | 34.01 | 2.08x |
| Transient Resolve | 22.34 | 62.51 | 2.80x |
| Scoped Resolve | 26.55 | 83.73 | 3.15x |
| Complex (3 deps) | 28.56 | 120.17 | 4.21x |

These numbers were captured from the published Native AOT executable at:

- `benchmarks/Pico.DI.Benchmarks/publish-win-x64/Pico.DI.Benchmarks.exe`

## BenchmarkDotNet (managed) artifacts

BenchmarkDotNet was run in managed mode earlier and produced artifacts in the `BenchmarkDotNet.Artifacts/results` folder. Key files:

- [Service resolution report (GitHub markdown)](BenchmarkDotNet.Artifacts/results/Pico.DI.Benchmarks.ServiceResolutionBenchmarks-report-github.md)
- [Service resolution CSV] (BenchmarkDotNet.Artifacts/results/Pico.DI.Benchmarks.ServiceResolutionBenchmarks-report.csv)
- [Service resolution HTML] (BenchmarkDotNet.Artifacts/results/Pico.DI.Benchmarks.ServiceResolutionBenchmarks-report.html)
- [Container setup report (GitHub markdown)](BenchmarkDotNet.Artifacts/results/Pico.DI.Benchmarks.ContainerSetupBenchmarks-report-github.md)

Use these artifacts for more detailed tables, histograms and diagnostics.

## How to reproduce

Run managed BenchmarkDotNet (recommended for detailed reporting):

```powershell
dotnet run --project benchmarks/Pico.DI.Benchmarks -c Release -- --bdn
```

Publish Native AOT (example for `win-x64`) and run the published exe:

```powershell
dotnet publish benchmarks/Pico.DI.Benchmarks -c Release -r win-x64 --self-contained true -o benchmarks/Pico.DI.Benchmarks/publish-win-x64
.
benchmarks\Pico.DI.Benchmarks\publish-win-x64\Pico.DI.Benchmarks.exe
```

## Notes

- The managed BenchmarkDotNet runs generate richer diagnostics (histograms, allocations, CI) and are preserved in `BenchmarkDotNet.Artifacts/results`.
- The Native AOT run uses a manual stopwatch fallback to avoid CommandLineParser / BenchmarkSwitcher issues under AOT; it gives quick, reproducible comparisons but lacks BenchmarkDotNet's diagnostics.

If you want, I can: 1) commit this report, 2) attach the CSV/HTML into a release folder, or 3) produce a compact CSV from the AOT numbers. Reply with `1` to commit this report, `2` to add artifacts, or `3` to create CSV.
