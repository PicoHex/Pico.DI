# 🚀 Pico.DI

> **Zero-reflection, AOT-native dependency injection for .NET — because your IoC container shouldn't be the bottleneck.**

[![NuGet](https://img.shields.io/nuget/v/Pico.DI.svg)](https://www.nuget.org/packages/Pico.DI)
[![License](https://img.shields.io/github/license/PicoHex/Pico.DI)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com)

```
┌─────────────────────────────────────────────────────────────┐
│  Pico.DI: Compile-time DI that doesn't suck                │
│  ✓ Zero reflection at runtime                              │
│  ✓ Native AOT compatible                                   │
│  ✓ Up to 5x faster than Microsoft.DI                       │
│  ✓ Zero GC allocations on hot paths                        │
└─────────────────────────────────────────────────────────────┘
```

## 🎯 Why Pico.DI?

| Feature | Pico.DI | Microsoft.DI |
|---------|---------|--------------|
| **Reflection** | ❌ None | ✅ Heavy |
| **AOT Support** | ✅ Native | ⚠️ Limited |
| **Factory Generation** | Compile-time | Runtime |
| **Circular Dependency Detection** | Compile-time error | Runtime crash |
| **GC Pressure** | Zero on resolution | Allocations |

### The Problem with Traditional DI

```csharp
// Microsoft.DI at runtime:
// 1. Reflection to find constructors
// 2. Expression tree compilation
// 3. Dynamic delegate generation
// 4. Runtime type checking
// Result: ~700ns per transient resolution 😴
```

### The Pico.DI Solution

```csharp
// Pico.DI at compile-time:
// Source Generator scans your code → Generates static factories
// Result: ~16-45ns per resolution 🚀 (that's 3-5x faster)
```

## ⚡ Quick Start

### Installation

```bash
dotnet add package Pico.DI
```

> 📦 Just install `Pico.DI` — it pulls in the source generator and abstractions automatically.

### Package Architecture

Pico.DI ships as three NuGet packages with a clean dependency chain:

```
Pico.DI  →  Pico.DI.Gen  →  Pico.DI.Abs
(runtime)   (source gen)    (interfaces)
```

| Package | Install when… | What you get |
|---------|--------------|-------------|
| **Pico.DI** | You're building an **application** | Runtime container + source generator + abstractions (all transitive) |
| **Pico.DI.Gen** | You're building a **library/extension** that needs compile-time code generation | Source generator + abstractions (no runtime container) |
| **Pico.DI.Abs** | You're building a **library/extension** that only needs the interfaces | Interfaces and base types only |

### Basic Usage

```csharp
using Pico.DI;
using Pico.DI.Abs;

// Define your services
public interface IGreeter { string Greet(string name); }
public class Greeter : IGreeter 
{
    public string Greet(string name) => $"Hello, {name}!";
}

// Register services - Source Generator does the heavy lifting
var container = new SvcContainer();
container.RegisterSingleton<IGreeter, Greeter>();
container.Build();

// Resolve
using var scope = container.CreateScope();
var greeter = scope.GetService<IGreeter>();
Console.WriteLine(greeter.Greet("World")); // Hello, World!
```

### Registration Methods

```csharp
var container = new SvcContainer();

// Lifetime options
container.RegisterTransient<IService, ServiceImpl>();     // New instance every time
container.RegisterScoped<IService, ServiceImpl>();        // One per scope
container.RegisterSingleton<IService, ServiceImpl>();     // One for app lifetime

// Factory registration (explicit control)
container.RegisterSingleton<IService>(sp => new ServiceImpl(
    sp.GetService<IDependency>()
));

// Instance registration
container.RegisterSingle<IConfig>(new AppConfig());

// Open generics
container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));

// Batch registration
container.RegisterRange(descriptors);

container.Build(); // Freeze & optimize
```

### Dependency Injection

```csharp
public class OrderService(
    IOrderRepository repo,      // ← Injected
    ILogger logger,             // ← Injected
    IPaymentGateway gateway)    // ← Injected
{
    public void ProcessOrder(Order order) { /* ... */ }
}

// Source Generator automatically detects constructor parameters
// and generates: sp => new OrderService(
//     sp.GetService<IOrderRepository>(),
//     sp.GetService<ILogger>(),
//     sp.GetService<IPaymentGateway>())
```

## 📊 Benchmarks

**Environment:** .NET 10.0 | Windows | x64 | **Native AOT** | 100 samples × 10,000 iterations

### Overall Summary

```
╔═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╗
║  Pico.DI wins:  18 / 18 scenarios                                                                             ║
║  Average speedup: 3.04x faster                                                                                ║
║  GC allocations: ZERO                                                                                         ║
╚═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╝
```

### By Service Complexity

#### NoDependency (Simple service)

| Library | Lifetime  | Avg (ns) | P50 (ns) | P90 (ns) | P95 (ns) | P99 (ns) | CPU Cycle | Speedup |
|---------|-----------|----------|----------|----------|----------|----------|-----------|---------|
| **Pico.DI** | Transient | 11.7 | 11.6 | 11.9 | 12.0 | 13.7 | 29 | **4.57x** 🔥 |
| MS.DI | Transient | 53.7 | 51.7 | 60.0 | 65.2 | 73.6 | 130 | baseline |
| **Pico.DI** | Scoped | 24.3 | 23.8 | 24.9 | 26.9 | 31.8 | 60 | **3.10x** |
| MS.DI | Scoped | 75.3 | 73.5 | 74.3 | 76.2 | 81.6 | 184 | baseline |
| **Pico.DI** | Singleton | 10.0 | 9.5 | 12.1 | 13.3 | 13.5 | 25 | **3.86x** |
| MS.DI | Singleton | 38.4 | 36.4 | 46.1 | 51.2 | 52.7 | 96 | baseline |

#### SingleDependency (Service with 1 dependency)

| Library | Lifetime  | Avg (ns) | P50 (ns) | P90 (ns) | P95 (ns) | P99 (ns) | CPU Cycle | Speedup |
|---------|-----------|----------|----------|----------|----------|----------|-----------|---------|
| **Pico.DI** | Transient | 31.5 | 31.1 | 32.2 | 32.6 | 34.8 | 79 | **3.24x** |
| MS.DI | Transient | 102.3 | 102.0 | 104.9 | 105.7 | 109.2 | 255 | baseline |
| **Pico.DI** | Scoped | 29.3 | 29.4 | 30.5 | 30.8 | 31.2 | 73 | **2.40x** |
| MS.DI | Scoped | 70.3 | 70.0 | 70.5 | 73.5 | 76.5 | 175 | baseline |
| **Pico.DI** | Singleton | 13.9 | 13.9 | 14.5 | 14.8 | 15.9 | 35 | **2.76x** |
| MS.DI | Singleton | 38.4 | 38.2 | 38.8 | 41.6 | 47.7 | 96 | baseline |

#### MultipleDependencies (Service with 2+ dependencies)

| Library | Lifetime  | Avg (ns) | P50 (ns) | P90 (ns) | P95 (ns) | P99 (ns) | CPU Cycle | Speedup |
|---------|-----------|----------|----------|----------|----------|----------|-----------|---------|
| **Pico.DI** | Transient | 55.4 | 53.1 | 63.4 | 68.0 | 73.6 | 138 | **2.94x** |
| MS.DI | Transient | 162.6 | 162.4 | 167.8 | 168.9 | 171.5 | 405 | baseline |
| **Pico.DI** | Scoped | 34.8 | 34.9 | 35.5 | 35.8 | 37.3 | 87 | **2.12x** |
| MS.DI | Scoped | 73.7 | 73.5 | 74.5 | 79.2 | 80.4 | 184 | baseline |
| **Pico.DI** | Singleton | 17.6 | 17.8 | 18.0 | 18.4 | 18.6 | 44 | **2.08x** |
| MS.DI | Singleton | 36.5 | 36.3 | 36.7 | 36.9 | 38.1 | 91 | baseline |

#### DeepChain (5-level dependency chain)

| Library | Lifetime  | Avg (ns) | P50 (ns) | P90 (ns) | P95 (ns) | P99 (ns) | CPU Cycle | Speedup |
|---------|-----------|----------|----------|----------|----------|----------|-----------|---------|
| **Pico.DI** | Transient | 89.4 | 88.6 | 91.1 | 92.4 | 97.9 | 223 | **2.83x** |
| MS.DI | Transient | 252.6 | 252.3 | 260.5 | 261.4 | 279.6 | 629 | baseline |
| **Pico.DI** | Scoped | 27.3 | 27.1 | 28.0 | 28.3 | 29.1 | 68 | **2.43x** |
| MS.DI | Scoped | 66.4 | 65.6 | 67.5 | 68.4 | 77.3 | 165 | baseline |
| **Pico.DI** | Singleton | 13.0 | 12.7 | 14.9 | 15.3 | 16.2 | 32 | **2.68x** |
| MS.DI | Singleton | 34.9 | 34.3 | 35.4 | 36.1 | 39.9 | 87 | baseline |

### Summary by Lifetime

| Lifetime | Average Speedup |
|----------|-----------------|
| **Transient** | **3.39x faster** 🔥 |
| Scoped | 2.51x faster |
| Singleton | 2.84x faster |

### Summary by Service Complexity

| Complexity | Average Speedup |
|------------|-----------------|
| NoDependency | 3.84x faster |
| SingleDependency | 2.80x faster |
| MultipleDependencies | 2.38x faster |
| DeepChain | 2.65x faster |

> 💡 **Why so fast?** Pico.DI generates inlined factory chains at compile-time. No reflection, no expression trees, no runtime codegen. Just pure, static method calls with `[MethodImpl(AggressiveInlining)]`.

## 🧠 How It Works

### Compile-Time Magic

```
Your Code                    Source Generator Output
─────────────────────────    ─────────────────────────────────────
container.RegisterSingleton  [ModuleInitializer]
  <IFoo, Foo>();             static void AutoRegister() {
                               SvcContainerAutoConfiguration
container.RegisterScoped       .RegisterConfigurator(Configure);
  <IBar, Bar>();             }
                             
scope.GetService<IFoo>();    static void Configure(ISvcContainer c) {
                               c.Register(new SvcDescriptor(
                                 typeof(IFoo),
                                 static sp => new Foo(),  // ← Compiled!
                                 SvcLifetime.Singleton));
                               c.Register(new SvcDescriptor(
                                 typeof(IBar),
                                 static sp => new Bar(),
                                 SvcLifetime.Scoped));
                             }
```

### Resolution Flow

```
GetService<IFoo>()
       │
       ▼
┌──────────────────────┐
│ FrozenDictionary     │  O(1) lookup
│ TryGetValue(typeof)  │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ SvcDescriptor        │
│ ├─ Factory: sp=>...  │  Pre-compiled static lambda
│ ├─ Lifetime: ...     │
│ └─ SingleInstance    │  For singletons
└──────────┬───────────┘
           │
           ▼
    Return instance
```

## 🔧 Advanced Usage

### Open Generics

```csharp
// Registration
container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));

// Usage - Source Generator detects all closed types
var userRepo = scope.GetService<IRepository<User>>();      // ✓ Generated
var orderRepo = scope.GetService<IRepository<Order>>();    // ✓ Generated
```

### Circular Dependency Detection

```csharp
// This won't compile - Source Generator catches it!
public class A(B b) { }
public class B(A a) { }  // Error PICO010: Circular dependency detected: A -> B -> A
```

### Testing

```csharp
[Test]
public async Task OrderService_Should_ProcessOrder()
{
    // Skip auto-registration, use mocks
    await using var container = new SvcContainer(autoConfigureFromGenerator: false);
    
    container.RegisterSingleton<IOrderRepository>(sp => new MockOrderRepo());
    container.RegisterSingleton<IOrderService, OrderService>();
    container.Build();
    
    using var scope = container.CreateScope();
    var service = scope.GetService<IOrderService>();
    // Assert...
}
```

### Multiple Implementations

```csharp
container.RegisterTransient<IPlugin, PluginA>();
container.RegisterTransient<IPlugin, PluginB>();
container.RegisterTransient<IPlugin, PluginC>();

// Get all implementations
var plugins = scope.GetServices<IPlugin>(); // [PluginA, PluginB, PluginC]

// Get last registered (override pattern)
var plugin = scope.GetService<IPlugin>();   // PluginC
```

## 📦 Packages

| Package | Target Audience | Dependencies |
|---------|----------------|-------------|
| [`Pico.DI`](https://www.nuget.org/packages/Pico.DI) | Application developers | → `Pico.DI.Gen` → `Pico.DI.Abs` |
| [`Pico.DI.Gen`](https://www.nuget.org/packages/Pico.DI.Gen) | Library / extension authors needing code generation | → `Pico.DI.Abs` |
| [`Pico.DI.Abs`](https://www.nuget.org/packages/Pico.DI.Abs) | Library / extension authors needing only interfaces | None |

**For most users:** just install `Pico.DI` — it transitively brings in the source generator (`Pico.DI.Gen`) and abstractions (`Pico.DI.Abs`).

**For library authors:** reference `Pico.DI.Gen` if you need compile-time source generation, or `Pico.DI.Abs` if you only need the DI interfaces. This avoids pulling the runtime container into your library's dependency tree.

## 🎮 Requirements

- **.NET 10.0+** (uses C# 14 extension types)
- **Roslyn 4.x+** (for source generation)

## ⚠️ Important: Understanding `autoConfigureFromGenerator`

### The Two Registration Modes

```csharp
// Mode 1: Auto-configuration (default)
var container = new SvcContainer();  // autoConfigureFromGenerator = true

// Mode 2: Manual configuration
var container = new SvcContainer(autoConfigureFromGenerator: false);
```

### How Registration Actually Works

**Placeholder methods** (depend on Source Generator):

```csharp
container.RegisterTransient<IFoo, Foo>();      // ⚠️ This is a PLACEHOLDER!
container.RegisterScoped<IBar, Bar>();         // ⚠️ Actually does nothing at runtime
container.RegisterSingleton<IBaz, Baz>();      // ⚠️ Source Generator intercepts these
```

**Factory methods** (real runtime registration):

```csharp
container.RegisterTransient<IFoo>(sp => new Foo());      // ✅ Real registration
container.RegisterScoped<IBar>(sp => new Bar());         // ✅ Works without Source Generator
container.RegisterSingleton<IBaz>(sp => new Baz());      // ✅ Always works
container.RegisterSingle<IConfig>(new Config());         // ✅ Instance registration
```

### The Limitation

| `autoConfigureFromGenerator` | Placeholder Methods | Factory Methods |
|------------------------------|--------------------:|----------------:|
| `true` (default) | ✅ Work (via Source Generator) | ✅ Work |
| `false` | ❌ **Do nothing!** | ✅ Work |

### When to Use `false`

```csharp
// ✅ Unit testing with mocks - use factory methods only
await using var container = new SvcContainer(autoConfigureFromGenerator: false);
container.RegisterSingleton<IRepo>(sp => new MockRepo());  // Factory method
container.RegisterSingleton<IService, MyService>();        // ❌ Won't work!
container.Build();

// ✅ Integration testing - apply generated config then override
await using var container = new SvcContainer(autoConfigureFromGenerator: false);
container.ConfigureGeneratedServices();                    // Manually apply
container.RegisterSingleton<IExternal>(sp => new Mock()); // Override specific services
```

### TL;DR

> 🚨 **If `autoConfigureFromGenerator = false`, you MUST use factory methods for registration.**
> Placeholder methods like `RegisterTransient<IFoo, Foo>()` will silently do nothing.

## 🤝 Contributing

PRs welcome! Please ensure:

- All tests pass (`dotnet test`)
- Benchmarks don't regress
- AOT compatibility maintained

## 📄 License

MIT License - See [LICENSE](LICENSE)

---

<p align="center">
  <b>Pico.DI</b> — Dependency Injection at the Speed of Light ⚡
</p>
