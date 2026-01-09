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
│  ✓ Up to 19x faster than Microsoft.DI                      │
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
// Result: ~12ns per resolution 🚀 (that's up to 19x faster)
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

**Environment:** .NET 10.0 | Windows | x64 | **Native AOT** | 25 samples × 10,000 iterations

### Overall Summary

```
╔══════════════════════════════════════════════════════════════╗
║  Pico.DI wins:  12 / 12 scenarios                            ║
║  Average speedup: 3.67x faster                               ║
║  GC allocations: ZERO (vs Gen0+12 for MS.DI)                 ║
╚══════════════════════════════════════════════════════════════╝
```

### Scope Creation

| Container | Avg (ns/op) | P50 | P99 | Speedup |
|-----------|-------------|-----|-----|---------|
| **Pico.DI** | 60 | 60 | 98 | **1.4x faster** |
| MS.DI | 87 | 81 | 147 | baseline |

### Single Resolution

| Container | Lifetime | Avg (ns/op) | P50 | Speedup |
|-----------|----------|-------------|-----|---------|
| **Pico.DI** | Transient | 12 | 12 | **12x faster** 🔥 |
| MS.DI | Transient | 140 | 127 | baseline |
| **Pico.DI** | Scoped | 12 | 12 | **4.8x faster** |
| MS.DI | Scoped | 59 | 59 | baseline |
| **Pico.DI** | Singleton | 12 | 12 | **2.6x faster** |
| MS.DI | Singleton | 33 | 32 | baseline |

### Multiple Resolutions (1M ops, hot path)

| Container | Lifetime | Avg (ns/op) | P50 | Speedup |
|-----------|----------|-------------|-----|---------|
| **Pico.DI** | Transient | 10 | 10 | **14x faster** 🔥 |
| MS.DI | Transient | 137 | 136 | baseline |
| **Pico.DI** | Scoped | 10 | 10 | **6.3x faster** |
| MS.DI | Scoped | 61 | 61 | baseline |
| **Pico.DI** | Singleton | 10 | 10 | **3x faster** |
| MS.DI | Singleton | 30 | 29 | baseline |

### Deep Dependency Chain (5 levels)

| Container | Lifetime | Avg (ns/op) | P50 | Speedup |
|-----------|----------|-------------|-----|---------|
| **Pico.DI** | Transient | 12 | 12 | **19x faster** 🔥🔥 |
| MS.DI | Transient | 223 | 218 | baseline |
| **Pico.DI** | Scoped | 12 | 12 | **5.2x faster** |
| MS.DI | Scoped | 62 | 60 | baseline |
| **Pico.DI** | Singleton | 12 | 12 | **2.6x faster** |
| MS.DI | Singleton | 32 | 32 | baseline |

### GC Pressure (1M transient resolutions)

| Container | Gen0 Collections | Result |
|-----------|-----------------|--------|
| **Pico.DI** | 0 | **Zero GC** ✨ |
| MS.DI | +12 | Allocations |

> 💡 **Why so fast?** Pico.DI generates inlined factory chains at compile-time. No reflection, no expression trees, no runtime codegen. Just pure, static method calls.

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
