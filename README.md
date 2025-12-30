```
 ____  _             ____ ___ 
|  _ \(_) ___ ___   |  _ \_ _|
| |_) | |/ __/ _ \  | | | | | 
|  __/| | (_| (_) |_| |_| | | 
|_|   |_|\___\___/(_)____/___|
                              
Zero-Reflection DI for .NET 10+ | Native AOT | Edge Ready
```

<div align="center">

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 14](https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![AOT](https://img.shields.io/badge/Native_AOT-✓-success?style=for-the-badge)]()
[![Trim](https://img.shields.io/badge/TrimMode-full-blue?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge)](LICENSE)

**Compile-time DI that actually works with AOT.**

[TL;DR](#-tldr) • [Why](#-why-another-di) • [Install](#-install) • [Platforms](#-platforms) • [Docs](#-docs) • [Internals](#-internals)

</div>

---

## ⚡ TL;DR

```bash
dotnet add package Pico.DI
dotnet add package Pico.DI.Gen
```

```csharp
var container = new SvcContainer();
container
    .RegisterSingleton<ILogger, ConsoleLogger>()
    .RegisterScoped<IRepository, SqlRepository>()
    .RegisterTransient<IService, MyService>()
    .ConfigureGeneratedServices();  // 🔮 Magic happens here

using var scope = container.CreateScope();
var svc = scope.GetService<IService>();  // Zero reflection. AOT safe.
```

**That's it.** Source generator handles the rest at compile time.

Important: Pico.DI requires the source generator (Pico.DI.Gen) to emit compile-time factories. If the generator did not run for your project, or you do not call `ConfigureGeneratedServices()`, the placeholder registration overloads that rely on generated code will throw `SourceGeneratorRequiredException` at runtime. For tests or manual setup you can use the factory overloads (for example, `RegisterSingleton<TService>(Func<ISvcScope,TService> factory)`) or enable the generator in your build.

Note: The generated `ConfigureGeneratedServices()` now attempts to call `Build()` on the concrete `SvcContainer` (when available) to freeze registrations and enable the optimized `SvcScope` lookup path.

---

## 🤔 Why Another DI?

```
┌─────────────────────────────────────────────────────────────┐
│  Traditional DI                    Pico.DI                  │
├─────────────────────────────────────────────────────────────┤
│  Runtime reflection         →      Compile-time codegen    │
│  Slow cold start            →      Instant startup         │
│  Breaks with AOT            →      Native AOT ready        │
│  Trimmer removes types      →      TrimMode=full safe      │
│  Runtime exceptions         →      Compile-time errors     │
│  ~500KB+ dependencies       →      ~15KB, zero deps        │
└─────────────────────────────────────────────────────────────┘
```

### Benchmarks (merged: BenchmarkDotNet + Native AOT friendly runner)

We consolidated benchmarking into a single project: `benchmarks/Pico.DI.Benchmarks`.

Two modes are supported:

- **Managed BenchmarkDotNet** (recommended for detailed reports): runs the `ContainerSetupBenchmarks` and `ServiceResolutionBenchmarks` with BenchmarkDotNet and generates rich artifacts (CSV, HTML, markdown).
- **Native AOT friendly manual runner** (default when running the published AOT exe): a lightweight Stopwatch-based runner that reproduces comparable AOT timings without relying on CommandLineParser/BenchmarkSwitcher (which is not AOT-friendly).

Example AOT header from a run (machine: 12th Gen Intel Core i7-1260P, Windows 11):

```
Pico.DI vs MS.DI - Native AOT Benchmark
Runtime: .NET 10.0.1 — AOT: Yes (Native AOT)
```

Quick AOT sample results (example run):

| Metric | Pico.DI (ns) | MS.DI (ns) | Speedup |
|---|---:|---:|---:|
| Container Setup | 778.82 | 1527.70 | 1.96x |
| Singleton Resolve | 16.38 | 34.01 | 2.08x |
| Transient Resolve | 22.34 | 62.51 | 2.80x |
| Scoped Resolve | 26.55 | 83.73 | 3.15x |
| Complex (3 deps) | 28.56 | 120.17 | 4.21x |

These numbers were produced by the AOT-friendly runner inside the published executable.

Artifacts and reproduction

- Managed BenchmarkDotNet artifacts are produced under: `BenchmarkDotNet.Artifacts/results/` inside the `benchmarks/Pico.DI.Benchmarks` run directory (CSV/HTML/markdown).
- Published Native AOT executable (example output path): `benchmarks/Pico.DI.Benchmarks/publish-win-x64/`.

**CI / Native AOT:** See `docs/CI-AOT.md` for a Windows (GitHub Actions) example and runner prerequisites (MSVC linkers, `VsDevCmd.bat`, `IlcUseEnvironmentalTools=true`, `TrimMode=full`).

To reproduce (managed, detailed):

```powershell
dotnet run --project benchmarks/Pico.DI.Benchmarks -c Release -- --bdn
```

To publish and run the Native AOT executable (example for `win-x64`):

```powershell
dotnet publish benchmarks/Pico.DI.Benchmarks -c Release -r win-x64 --self-contained true -o benchmarks/Pico.DI.Benchmarks/publish-win-x64
.
benchmarks\Pico.DI.Benchmarks\publish-win-x64\Pico.DI.Benchmarks.exe
```

Notes:

- The BenchmarkDotNet managed runs provide richer diagnostics (histograms, CI, allocation measurements) and should be used for formal reporting.
- The Native AOT published exe uses a manual Stopwatch fallback to obtain AOT timings without depending on CommandLineParser/BenchmarkSwitcher, which are incompatible with Native AOT by default.


---

## 📦 Install

### Option A: CLI

```bash
dotnet add package Pico.DI
dotnet add package Pico.DI.Gen
```

```
 ____  _             ____ ___ 
|  _ \(_) ___ ___   |  _ \_ _|
| |_) | |/ __/ _ \  | | | | | 
|  __/| | (_| (_) |_| |_| | | 
|_|   |_|\___\___/(_)____/___|
                              
Pico.DI — The Compile-Time DI for .NET 10+ | Native AOT | Cloud | Edge | Embedded
```

> **No runtime reflection. No runtime surprises. Just pure, static, geek-approved dependency injection.**

---

## 🚀 Why Pico.DI?

- **Zero Reflection:** All factories are generated at compile time. No `Activator`, no `Type.GetType`, no `MethodInfo.Invoke`. Ever.
- **Compile-Time Cycle Detection:** Circular dependencies? Pico.DI’s source generator will catch them before you even hit F5.
- **Open Generics:** Register and resolve `IRepository<T>`, `IService<T1, T2>`, etc. All closed generic factories are generated at build time.
- **IEnumerable<T> Injection:** Register multiple implementations, inject them as `IEnumerable<T>`. All resolved statically.
- **AOT & Trimming Safe:** Designed for .NET Native AOT, IL trimming, and minimal binary size.
- **Cloud, Edge, Embedded Ready:** No runtime magic, no dynamic code, no surprises. Works everywhere .NET runs.

---

## 🦾 How It Works

1. **You write this:**

```csharp
var container = new SvcContainer();
container
.RegisterSingleton<ILogger, ConsoleLogger>()
.RegisterTransient<IGreeter, Greeter>()
.RegisterScoped<GreetingService>()
.ConfigureGeneratedServices(); // 🔮 Source generator magic
```

1. **Source generator scans your registrations at build time:**

- Analyzes all constructors, dependencies, and open generics.
- Emits explicit, static factory code for every service, every closed generic, every `IEnumerable<T>`.

1. **At runtime:**

- `GetService<T>()` is just a direct delegate call. No reflection, no runtime type discovery.
- Cycles? Impossible. The generator already failed your build if you had any.

---

## 🧬 Example

```csharp
// Service registration
container
  .RegisterSingleton<ILogger, ConsoleLogger>()
  .RegisterTransient<IGreeter, Greeter>()
  .RegisterScoped<GreetingService>()
  .ConfigureGeneratedServices();

// Usage
using var scope = container.CreateScope();
var svc = scope.GetService<GreetingService>();
svc.SayHello("World");
```

```csharp
// Service implementation
public class GreetingService(IGreeter greeter, ILogger logger)
{
  public void SayHello(string name)
  {
    logger.Log($"Greeting {name}");
    Console.WriteLine(greeter.Greet(name));
  }
}
```

---

## 🧩 Features

- **Constructor injection only** (no property injection, no runtime hacks)
- **Open generics**: Register and resolve `IService<T>`, `IRepository<TKey, TValue>`, etc.
- **IEnumerable<T>**: Register multiple `IHandler`, inject as `IEnumerable<IHandler>`
- **Compile-time diagnostics**:
  - `PICO001`: Service not registered
  - `PICO002`: Circular dependency detected
  - `PICO003`: Abstract type as implementation
  - `PICO004`: No public constructor
- **AOT/Trimming**: 100% compatible, no dynamic code, no reflection roots needed
- **Minimal runtime**: ~13KB core, ~25KB generator, zero dependencies

---

## 🌍 Where to Use

- **Cloud microservices**: Fast cold start, no runtime surprises
- **Edge computing**: Small, static, reliable
- **Embedded/IoT**: No dynamic code, works on all .NET 10+ targets
- **Anywhere you want DI without the bloat**

---

## 🛠️ Install

```bash
dotnet add package Pico.DI
dotnet add package Pico.DI.Gen
```

---

## 🧙 Internals

- **Source generator**: Scans all `Register*` calls, emits static factories, detects cycles, generates `ConfigureGeneratedServices()`.
- **No runtime registration**: All registrations are for the generator; at runtime, only prebuilt factories are used.
- **No runtime reflection**: Not even a little bit.

---

## ⚠️ Limitations

- No property injection
- No optional constructor parameters
- .NET 10+ and C# 14+ only

---

## 📄 License

MIT — Use it, fork it, hack it.

---

> “The best DI is the one you don’t notice at runtime.” — Ancient Geek Proverb

---

**Cloud • Edge • Embedded • Everywhere .NET runs**
