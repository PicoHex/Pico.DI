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

### Benchmarks (Native AOT, .NET 10, Dec 2025)

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7462/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-1260P 2.10GHz, 1 CPU, 16 logical and 12 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3
```

| Method                      | Mean       | Error    | StdDev    | Median     | Rank | Gen0   | Gen1   | Allocated |
|---------------------------- |-----------:|---------:|----------:|-----------:|-----:|-------:|-------:|----------:|
| &#39;Pico.DI - Container Setup&#39; |   382.8 ns | 15.55 ns |  44.62 ns |   376.4 ns |    1 | 0.3295 | 0.0029 |   3.03 KB |
| &#39;MS.DI - Container Setup&#39;   | 1,034.5 ns | 37.98 ns | 111.38 ns | 1,004.4 ns |    2 | 0.6618 | 0.0935 |   6.09 KB |

| Method                      | Mean     | Error    | StdDev   | Median   | Rank | Gen0   | Allocated |
|---------------------------- |---------:|---------:|---------:|---------:|-----:|-------:|----------:|
| &#39;Pico.DI - Deep (5 levels)&#39; | 19.06 ns | 0.519 ns | 1.522 ns | 18.24 ns |    2 | 0.0025 |      24 B |
| &#39;MS.DI - Deep (5 levels)&#39;   | 10.47 ns | 0.337 ns | 0.994 ns | 10.18 ns |    1 | 0.0025 |      24 B |

| Method                   | Mean      | Error    | StdDev    | Median   | Rank | Gen0   | Gen1   | Allocated |
|------------------------- |----------:|---------:|----------:|---------:|-----:|-------:|-------:|----------:|
| &#39;Pico.DI - Create Scope&#39; | 100.09 ns | 3.698 ns | 10.844 ns | 96.60 ns |    2 | 0.1130 | 0.0004 |    1064 B |
| &#39;MS.DI - Create Scope&#39;   |  17.60 ns | 0.687 ns |  2.016 ns | 16.65 ns |    1 | 0.0136 |      - |     128 B |

| Method                       | Mean      | Error     | StdDev    | Median    | Rank | Gen0   | Allocated |
|----------------------------- |----------:|----------:|----------:|----------:|-----:|-------:|----------:|
| &#39;Pico.DI - Singleton&#39;        | 10.492 ns | 0.3023 ns | 0.8867 ns | 10.166 ns |    2 |      - |         - |
| &#39;MS.DI - Singleton&#39;          |  6.574 ns | 0.2187 ns | 0.6204 ns |  6.295 ns |    1 |      - |         - |
| &#39;Pico.DI - Transient&#39;        | 12.882 ns | 0.3698 ns | 1.0788 ns | 12.307 ns |    3 | 0.0025 |      24 B |
| &#39;MS.DI - Transient&#39;          | 10.564 ns | 0.3515 ns | 1.0364 ns | 10.249 ns |    2 | 0.0025 |      24 B |
| &#39;Pico.DI - Scoped&#39;           | 15.347 ns | 0.3541 ns | 1.0045 ns | 15.015 ns |    4 |      - |         - |
| &#39;MS.DI - Scoped&#39;             | 30.385 ns | 0.7682 ns | 2.2531 ns | 29.792 ns |    7 |      - |         - |
| &#39;Pico.DI - Complex (3 deps)&#39; | 16.938 ns | 0.4721 ns | 1.3919 ns | 16.270 ns |    5 |      - |         - |
| &#39;MS.DI - Complex (3 deps)&#39;   | 27.197 ns | 0.6264 ns | 1.8273 ns | 26.471 ns |    6 |      - |         - |

**📊 Analysis:**

- **Singleton:** MS.DI is faster (6.57ns) than Pico.DI (10.49ns).
- **Transient:** MS.DI (10.56ns) is faster than Pico.DI (12.88ns).
- **Scoped:** Pico.DI (15.35ns) is faster than MS.DI (30.39ns).
- **Complex (3 deps):** Pico.DI (16.94ns) is much faster than MS.DI (27.20ns).
- **Deep (5 levels):** MS.DI (10.47ns) is faster than Pico.DI (19.06ns).

- **Conclusion:** Pico.DI achieves competitive performance with MS.DI, especially in scoped and complex scenarios, while providing zero-reflection, compile-time safety, and AOT compatibility.
- **Key Advantage:** Pico.DI provides **zero reflection**, **compile-time cycle detection**, **AOT safety**.
- **Binary Size:** ~2.1 MB AOT benchmark app (includes both DI frameworks + test harness)

*Run AOT benchmark: `cd benchmarks/Pico.DI.AotBenchmark && dotnet publish -c Release -r win-x64 && bin\Release\net10.0\win-x64\publish\Pico.DI.AotBenchmark.exe`*

**📊 Analysis:**

- **Singleton:** MS.DI is faster (6.57ns) than Pico.DI (10.49ns).
- **Transient:** MS.DI (10.56ns) is faster than Pico.DI (12.88ns).
- **Scoped:** Pico.DI (15.35ns) is faster than MS.DI (30.39ns).
- **Complex (3 deps):** Pico.DI (16.94ns) is much faster than MS.DI (27.20ns).
- **Deep (5 levels):** MS.DI (10.47ns) is faster than Pico.DI (19.06ns).

- **Conclusion:** Pico.DI achieves competitive performance with MS.DI, especially in scoped and complex scenarios, while providing zero-reflection, compile-time safety, and AOT compatibility.
- **Key Advantage:** Pico.DI provides **zero reflection**, **compile-time cycle detection**, **AOT safety**.
- **Binary Size:** ~2.1 MB AOT benchmark app (includes both DI frameworks + test harness)

*Run AOT benchmark: `cd benchmarks/Pico.DI.AotBenchmark && dotnet publish -c Release -r win-x64 && bin\Release\net10.0\win-x64\publish\Pico.DI.AotBenchmark.exe`*

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
