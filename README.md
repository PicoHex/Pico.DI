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
    .ConfigureGeneratedServices();  // 🔮 Call once after all Register* calls (may call Build())

using var scope = container.CreateScope();
var svc = scope.GetService<IService>();  // Zero reflection. AOT safe.
```

**That's it.** Source generator handles the rest at compile time.

Important: Pico.DI requires the source generator (Pico.DI.Gen) to emit compile-time factories. If the generator did not run for your project, or you do not call `ConfigureGeneratedServices()`, the placeholder registration overloads that rely on generated code will throw `SourceGeneratorRequiredException` at runtime. For tests or manual setup you can use the factory overloads (for example, `RegisterSingleton<TService>(Func<ISvcScope,TService> factory)`) or enable the generator in your build.

Note: The generated `ConfigureGeneratedServices()` now attempts to call `Build()` on the concrete `SvcContainer` (when available) to freeze registrations and enable the optimized `SvcScope` lookup path. Call `ConfigureGeneratedServices()` after you have completed all `Register*` calls; it returns the container and may call `Build()` on the concrete container to optimize lookups and freeze registrations. Attempting to register services after this point will throw an exception.

## 🧪 Testing without the source generator

If you run tests without the source generator, you have two options:

- Use the factory overloads (for example, `RegisterSingleton<TService>(Func<ISvcScope, TService> factory)`) to perform registrations manually in tests.
- Add `Pico.DI.Gen` as an analyzer to the projects that contain your `Register*` calls (example: add a ProjectReference to `src\Pico.DI.Gen\Pico.DI.Gen.csproj` with `OutputItemType="Analyzer"` in your test project's .csproj). The repository contains examples in `tests/*.csproj`.

Note: The fallback (testing) path uses generic Activator-based construction and is slower than generated factories.

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
- **Native AOT Stopwatch Runner** (default when running the published AOT exe): a lightweight Stopwatch-based comparison benchmark against Microsoft.Extensions.DependencyInjection that works with Native AOT.

#### Latest Benchmark Results (Native AOT, TrimMode=full)

**Pico.DI wins 14 out of 15 scenarios** against Microsoft.Extensions.DependencyInjection:

| Scenario | Lifetime | Pico.DI | Ms.DI | Ratio | Winner |
|----------|----------|--------:|------:|------:|--------|
| **ContainerSetup** | Transient | 1141 ns | 1416 ns | 0.81x | ✅ Pico.DI |
| | Scoped | 834 ns | 1318 ns | 0.63x | ✅ Pico.DI |
| | Singleton | 833 ns | 1318 ns | 0.63x | ✅ Pico.DI |
| **ScopeCreation** | Transient | 856 ns | 2003 ns | 0.43x | ✅ Pico.DI |
| | Scoped | 870 ns | 1843 ns | 0.47x | ✅ Pico.DI |
| | Singleton | 806 ns | 1908 ns | 0.42x | ✅ Pico.DI |
| **SingleResolution** | Transient | 94 ns | 261 ns | 0.36x | ✅ Pico.DI |
| | Scoped | 528 ns | 403 ns | 1.31x | Ms.DI |
| | Singleton | 58 ns | 265 ns | **0.22x** | ✅ Pico.DI |
| **MultipleResolutions** | Transient | 5609 ns | 18914 ns | 0.30x | ✅ Pico.DI |
| | Scoped | 2582 ns | 7452 ns | 0.35x | ✅ Pico.DI |
| | Singleton | 1188 ns | 3525 ns | 0.34x | ✅ Pico.DI |
| **DeepDependencyChain** | Transient | 148 ns | 392 ns | 0.38x | ✅ Pico.DI |
| | Scoped | 425 ns | 1130 ns | 0.38x | ✅ Pico.DI |
| | Singleton | 56 ns | 151 ns | 0.37x | ✅ Pico.DI |

**Key Highlights:**
- **Singleton resolution: 4.6x faster** (58ns vs 265ns)
- **Transient resolution: 2.8x faster** (94ns vs 261ns)
- **Scope creation: 2.3x faster** (856ns vs 2003ns)
- **Deep dependency chain (5 levels): 2.7x faster** (56ns vs 151ns for singleton)

#### Running Benchmarks

**Native AOT (recommended for real-world performance):**

```powershell
# Build and publish AOT executable
dotnet publish benchmarks/Pico.DI.Benchmarks -c Release -r win-x64 --self-contained

# Run the benchmark
./benchmarks/Pico.DI.Benchmarks/bin/Release/net10.0/win-x64/publish/Pico.DI.Benchmarks.exe
```

**BenchmarkDotNet (for detailed statistical analysis):**

```powershell
dotnet run --project benchmarks/Pico.DI.Benchmarks -c Release -- --bdn
```

#### Advanced: Generated Typed Resolvers

For maximum performance, Pico.DI.Gen generates typed resolver methods that bypass dictionary lookup entirely:

```csharp
// Standard resolution (uses FrozenDictionary lookup)
var logger = scope.GetService<ILogger>();

// Generated typed resolver (direct factory call, ~20% faster)
var logger = Pico.DI.Gen.GeneratedServiceRegistrations.Resolve.MyNamespace_ILogger(scope);
```

**CI / Native AOT:** See `docs/CI-AOT.md` for a Windows (GitHub Actions) example and runner prerequisites (MSVC linkers, `VsDevCmd.bat`, `IlcUseEnvironmentalTools=true`, `TrimMode=full`).

---

## 📦 Install

### Option A: CLI

```bash
dotnet add package Pico.DI
dotnet add package Pico.DI.Gen
```

**Tip:** Add `Pico.DI.Gen` to the projects that contain your `Register*` calls — install it as a NuGet analyzer or reference the generator project as an analyzer in test projects (see `tests/*.csproj` for examples).

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

**Note:** Call `ConfigureGeneratedServices()` only after you have finished registering services; the generated method may call `Build()` on the concrete container to freeze registrations and enable optimal lookup paths. Registering services after `ConfigureGeneratedServices()` is called will throw an exception because the container may be frozen.

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

> **Note:** Call `ConfigureGeneratedServices()` once after all `Register*` calls; it returns the container and may invoke `Build()` to freeze the registration state for optimal performance.
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
