<div align="center">

# Pico.DI

### Next-Generation Dependency Injection for Modern .NET Applications

**Compile-Time Code Generation | Zero Runtime Reflection | Native AOT Ready**

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 14](https://img.shields.io/badge/C%23-14-239120?style=flat-square&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![MIT License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![AOT Compatible](https://img.shields.io/badge/AOT-Compatible-success?style=flat-square)]()

---

[Getting Started](#-getting-started) • [Documentation](#-documentation) • [Architecture](#-architecture) • [Contributing](#-contributing)

</div>

---

## Overview

**Pico.DI** is an enterprise-grade, lightweight dependency injection container designed for high-performance .NET applications. By leveraging Roslyn Source Generators, Pico.DI eliminates runtime reflection overhead, making it the ideal choice for Native AOT deployments, microservices, and performance-critical systems.

### Why Pico.DI?

| Challenge | Traditional DI | Pico.DI Solution |
|-----------|---------------|------------------|
| **AOT Compatibility** | Runtime reflection fails in AOT | Compile-time factory generation |
| **Cold Start Performance** | Reflection-based scanning | Zero startup overhead |
| **Trimming Safety** | Broken by IL trimmer | Fully trim-compatible |
| **Compile-Time Validation** | Runtime exceptions | Roslyn Analyzer diagnostics |

---

## Key Features

<table>
<tr>
<td width="50%">

### Performance & Compatibility

- **Native AOT Support** — No runtime reflection
- **Compile-Time Factories** — Source Generator powered
- **Minimal Footprint** — Zero external dependencies
- **Trim-Safe** — Compatible with IL trimming

</td>
<td width="50%">

### Developer Experience

- **Fluent API** — Clean, chainable registration
- **Compile-Time Diagnostics** — Catch errors before runtime
- **Circular Dependency Detection** — Clear error messages
- **Async Disposal** — Full `IAsyncDisposable` support

</td>
</tr>
</table>

---

## 📦 Installation

```bash
dotnet add package Pico.DI
```

> **Note**: Package coming soon to NuGet.org

---

## 🚀 Getting Started

### Step 1: Define Services

```csharp
public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
}

public interface IGreeter
{
    string Greet(string name);
}

public class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

public class GreetingService(IGreeter greeter, ILogger logger)
{
    public void SayHello(string name)
    {
        logger.Log($"Generating greeting for: {name}");
        Console.WriteLine(greeter.Greet(name));
    }
}
```

### Step 2: Configure Container

```csharp
using Pico.DI;
using Pico.DI.Abs;
using Pico.DI.Gen;

public static class Startup
{
    public static ISvcContainer ConfigureServices()
    {
        var container = new SvcContainer();
        
        container
            .RegisterSingleton<ILogger, ConsoleLogger>()
            .RegisterTransient<IGreeter, Greeter>()
            .RegisterScoped<GreetingService>()
            .ConfigureGeneratedServices();  // Apply compile-time generated factories
            
        return container;
    }
}
```

### Step 3: Resolve & Execute

```csharp
using var container = Startup.ConfigureServices();
using var scope = container.CreateScope();

var service = scope.GetService<GreetingService>();
service.SayHello("Enterprise");
```

---

## 📖 Documentation

### Service Lifetimes

| Lifetime | Behavior | Use Case |
|----------|----------|----------|
| `Transient` | New instance per request | Stateless services, lightweight operations |
| `Scoped` | Single instance per scope | Request-scoped data, Unit of Work pattern |
| `Singleton` | Single instance application-wide | Configuration, caching, shared state |

```csharp
container
    .RegisterTransient<IEmailService, EmailService>()     // New instance each time
    .RegisterScoped<IUnitOfWork, UnitOfWork>()            // Per-scope instance
    .RegisterSingleton<IConfiguration, AppConfiguration>(); // Global singleton
```

### Registration Patterns

#### Type-Based Registration (AOT-Compatible)

```csharp
// Service → Implementation mapping
container.RegisterTransient<IService, ServiceImpl>();
container.RegisterScoped<IRepository, Repository>();
container.RegisterSingleton<ICache, MemoryCache>();

// Self-registration
container.RegisterScoped<MyService>();
```

#### Factory-Based Registration

```csharp
// Custom instantiation logic
container.RegisterScoped<IDbContext>(scope => 
    new AppDbContext(Configuration.ConnectionString));

// Conditional registration
container.RegisterSingleton<ILogger>(scope => 
    Environment.IsDevelopment() 
        ? new ConsoleLogger() 
        : new FileLogger());
```

#### Instance Registration

```csharp
// Pre-configured singleton
var config = new AppConfiguration { Environment = "Production" };
container.RegisterSingle<IConfiguration>(config);
```

#### Open Generic Registration

```csharp
// Register open generic types - automatically detected!
container.RegisterScoped(typeof(IRepository<>), typeof(Repository<>));
container.RegisterTransient(typeof(ICache<,>), typeof(MemoryCache<,>));
container.RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));

// Or use the unified Register method with explicit lifetime
container.Register(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Scoped);

// Resolve closed generic types
var userRepo = scope.GetService<IRepository<User>>();
var orderRepo = scope.GetService<IRepository<Order>>();
```

### Multiple Implementations

```csharp
// Register multiple handlers
container
    .RegisterSingleton<INotificationHandler>(s => new EmailHandler())
    .RegisterSingleton<INotificationHandler>(s => new SmsHandler())
    .RegisterSingleton<INotificationHandler>(s => new PushHandler());

// Resolve all implementations
var handlers = scope.GetServices<INotificationHandler>();
foreach (var handler in handlers)
{
    await handler.SendAsync(notification);
}
```

### Compile-Time Diagnostics

Pico.DI includes a Roslyn Analyzer that detects issues at compile time:

| Code | Severity | Description |
|------|----------|-------------|
| `PICO001` | Warning | Unregistered dependency detected |
| `PICO002` | Warning | Potential circular dependency |
| `PICO003` | Error | Cannot register abstract type as implementation |
| `PICO004` | Error | Implementation type has no public constructor |

---

## 🏗 Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Pico.DI                                 │
├─────────────────────────────────────────────────────────────────┤
│  Pico.DI.Gen          │ Source Generator & Roslyn Analyzer      │
│  (Compile-Time)       │ • Scans Register<T>() calls             │
│                       │ • Generates AOT-compatible factories    │
│                       │ • Emits compile-time diagnostics        │
├─────────────────────────────────────────────────────────────────┤
│  Pico.DI              │ Runtime Container Implementation        │
│  (Runtime)            │ • Service resolution & lifetime mgmt    │
│                       │ • Scope management & disposal           │
│                       │ • Circular dependency detection         │
├─────────────────────────────────────────────────────────────────┤
│  Pico.DI.Abs          │ Abstractions & Contracts                │
│  (Contracts)          │ • ISvcContainer, ISvcScope interfaces   │
│                       │ • SvcDescriptor, SvcLifetime types      │
└─────────────────────────────────────────────────────────────────┘
```

### How It Works

<table>
<tr>
<td width="50%">

**Your Code**

```csharp
container.RegisterSingleton<ILogger, ConsoleLogger>();
container.ConfigureGeneratedServices();
```

</td>
<td width="50%">

**Generated Code**

```csharp
container.Register(new SvcDescriptor(
    typeof(ILogger),
    static _ => new ConsoleLogger(),
    SvcLifetime.Singleton));
```

</td>
</tr>
</table>

---

## ⚠️ Considerations

| Aspect | Status | Notes |
|--------|--------|-------|
| Constructor Injection | ✅ Supported | Primary injection method |
| Property Injection | ❌ Not Supported | Use constructor injection |
| Optional Dependencies | ❌ Not Supported | All parameters must be registered |
| Lazy Resolution | ❌ Not Supported | Services resolved immediately |
| IServiceProvider Adapter | ✅ Supported | For framework integration |

---

## 🤝 Contributing

We welcome contributions from the community. Please read our contributing guidelines before submitting a pull request.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

<div align="center">

**Built with performance in mind for the modern .NET ecosystem**

</div>
