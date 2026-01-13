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

> 📦 `Pico.DI` includes both the runtime and source generator. That's it. One package.

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
║  Pico.DI wins:  12 / 12 scenarios                                                                             ║
║  Average speedup: 3.27x faster                                                                                ║
║  GC allocations: ZERO                                                                                         ║
╚═══════════════════════════════════════════════════════════════════════════════════════════════════════════════╝
```

### By Service Complexity

#### NoDependency (Simple service)

| Library | Lifetime  | Avg (ns) | P50 (ns) | P90 (ns) | P95 (ns) | P99 (ns) | CPU Cycle | Speedup |
|---------|-----------|----------|----------|----------|----------|----------|-----------|---------|
| **Pico.DI** | Transient | 21.3 | 18.3 | 28.4 | 31.2 | 33.9 | 68 | **5.38x** 🔥 |
| MS.DI | Transient | 114.6 | 105.5 | 151.4 | 153.6 | 162.1 | 365 | baseline |
| **Pico.DI** | Scoped | 37.8 | 33.8 | 48.5 | 53.7 | 62.1 | 121 | **2.99x** |
| MS.DI | Scoped | 113.0 | 103.6 | 144.1 | 153.0 | 172.5 | 360 | baseline |
| **Pico.DI** | Singleton | 16.2 | 14.9 | 20.9 | 22.4 | 24.2 | 51 | **3.87x** |
| MS.DI | Singleton | 62.7 | 56.8 | 78.3 | 100.6 | 105.3 | 200 | baseline |

#### SingleDependency (Service with 1 dependency)

| Library | Lifetime  | Avg (ns) | P50 (ns) | P90 (ns) | P95 (ns) | P99 (ns) | CPU Cycle | Speedup |
|---------|-----------|----------|----------|----------|----------|----------|-----------|---------|
| **Pico.DI** | Transient | 45.4 | 43.5 | 52.1 | 57.2 | 65.8 | 145 | **3.78x** |
| MS.DI | Transient | 171.4 | 164.0 | 192.1 | 208.9 | 235.7 | 547 | baseline |
| **Pico.DI** | Scoped | 39.9 | 36.4 | 52.5 | 54.6 | 65.0 | 127 | **2.77x** |
| MS.DI | Scoped | 110.4 | 101.6 | 128.3 | 143.7 | 196.2 | 352 | baseline |
| **Pico.DI** | Singleton | 17.7 | 16.8 | 19.1 | 21.4 | 29.5 | 56 | **3.36x** |
| MS.DI | Singleton | 59.5 | 54.1 | 75.9 | 81.7 | 94.8 | 189 | baseline |

#### MultipleDependencies (Service with 2+ dependencies)

| Library | Lifetime  | Avg (ns) | P50 (ns) | P90 (ns) | P95 (ns) | P99 (ns) | CPU Cycle | Speedup |
|---------|-----------|----------|----------|----------|----------|----------|-----------|---------|
| **Pico.DI** | Transient | 81.5 | 77.8 | 104.1 | 108.0 | 115.4 | 260 | **4.60x** 🔥 |
| MS.DI | Transient | 375.0 | 372.3 | 461.2 | 475.3 | 493.3 | 1185 | baseline |
| **Pico.DI** | Scoped | 43.8 | 41.9 | 49.2 | 52.4 | 69.1 | 136 | **2.21x** |
| MS.DI | Scoped | 97.1 | 93.7 | 110.6 | 118.6 | 128.6 | 307 | baseline |
| **Pico.DI** | Singleton | 22.2 | 20.1 | 26.1 | 31.8 | 48.2 | 66 | **2.36x** |
| MS.DI | Singleton | 52.3 | 50.3 | 60.5 | 65.0 | 78.0 | 158 | baseline |

#### DeepChain (5-level dependency chain)

| Library | Lifetime  | Avg (ns) | P50 (ns) | P90 (ns) | P95 (ns) | P99 (ns) | CPU Cycle | Speedup |
|---------|-----------|----------|----------|----------|----------|----------|-----------|---------|
| **Pico.DI** | Transient | 149.8 | 145.8 | 165.7 | 185.4 | 210.6 | 478 | **3.44x** |
| MS.DI | Transient | 515.7 | 501.4 | 569.8 | 583.6 | 641.9 | 1645 | baseline |
| **Pico.DI** | Scoped | 41.4 | 38.7 | 50.1 | 59.3 | 68.5 | 132 | **2.56x** |
| MS.DI | Scoped | 105.9 | 100.9 | 128.2 | 141.1 | 162.9 | 337 | baseline |
| **Pico.DI** | Singleton | 27.0 | 22.6 | 37.5 | 38.2 | 39.6 | 86 | **1.91x** |
| MS.DI | Singleton | 51.5 | 50.9 | 54.9 | 59.9 | 64.5 | 164 | baseline |

### Summary by Lifetime

| Lifetime | Average Speedup |
|----------|-----------------|
| **Transient** | **4.30x faster** 🔥 |
| Scoped | 2.63x faster |
| Singleton | 2.88x faster |

### Summary by Service Complexity

| Complexity | Average Speedup |
|------------|-----------------|
| NoDependency | 4.08x faster |
| SingleDependency | 3.30x faster |
| MultipleDependencies | 3.06x faster |
| DeepChain | 2.64x faster |

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

| Package | Description |
|---------|-------------|
| `Pico.DI` | Full package with runtime + source generator |
| `Pico.DI.Abs` | Abstractions only (for library authors) |

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
