# Pico.IoC

A lightweight, AOT-compatible IoC (Inversion of Control) container for .NET, powered by Source Generators.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## ✨ Features

- **AOT Compatible** - Uses Source Generators to create factory methods at compile time, no runtime reflection
- **Lightweight** - Minimal dependencies, simple API
- **Constructor Injection** - Automatic dependency resolution via constructor parameters
- **Circular Dependency Detection** - Detects and reports circular dependencies with clear error messages
- **Multiple Lifetimes** - Supports Transient, Scoped, and Singleton lifetimes
- **Multiple Implementations** - Register multiple implementations for the same service type
- **Fluent API** - Chain registrations for clean, readable code
- **Async Disposal** - Full support for `IAsyncDisposable`

## 📦 Installation

```bash
# Coming soon to NuGet
dotnet add package Pico.IoC
```

## 🚀 Quick Start

### 1. Define Your Services

```csharp
public interface IGreeter
{
    string Greet(string name);
}

public class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine($"[LOG] {message}");
}

public class GreetingService(IGreeter greeter, ILogger logger)
{
    public void SayHello(string name)
    {
        logger.Log($"Greeting {name}");
        Console.WriteLine(greeter.Greet(name));
    }
}
```

### 2. Register Services

```csharp
using Pico.IoC;
using Pico.IoC.Abs;
using Pico.IoC.Gen;

public static class ServiceConfig
{
    public static void ConfigureServices(ISvcContainer container)
    {
        // Type-based registration (scanned by Source Generator)
        container
            .RegisterSingleton<ILogger, ConsoleLogger>()
            .RegisterTransient<IGreeter, Greeter>()
            .RegisterScoped<GreetingService>();

        // Apply auto-generated factory registrations
        container.ConfigureGeneratedServices();
    }
}
```

### 3. Resolve and Use Services

```csharp
using var container = new SvcContainer();
ServiceConfig.ConfigureServices(container);

using var scope = container.CreateScope();
var greetingService = scope.GetService<GreetingService>();
greetingService.SayHello("World");
```

## 📖 Service Lifetimes

| Lifetime | Description |
|----------|-------------|
| **Transient** | A new instance is created every time the service is requested |
| **Scoped** | A single instance is created per scope |
| **Singleton** | A single instance is shared across all scopes |

### Examples

```csharp
// Transient - new instance each time
container.RegisterTransient<IGreeter, Greeter>();

// Scoped - one instance per scope
container.RegisterScoped<DbContext>();

// Singleton - one instance globally
container.RegisterSingleton<ILogger, ConsoleLogger>();
```

## 🔧 Registration Methods

### Type-Based Registration (AOT-Compatible)

These methods are placeholders scanned by the Source Generator. The actual registration with factory methods is generated at compile time.

```csharp
container.RegisterTransient<TService, TImplementation>();
container.RegisterScoped<TService, TImplementation>();
container.RegisterSingleton<TService, TImplementation>();

// Self-registration
container.RegisterTransient<TService>();
```

### Factory-Based Registration

For manual control or complex instantiation logic:

```csharp
container.RegisterTransient<IGreeter>(scope => new Greeter());
container.RegisterScoped<IDbContext>(scope => new DbContext(connectionString));
container.RegisterSingleton<ILogger>(scope => new ConsoleLogger());
```

### Instance Registration

Register a pre-created singleton instance:

```csharp
var logger = new ConsoleLogger();
container.RegisterSingle<ILogger>(logger);
```

## 🔄 Circular Dependency Detection

Pico.IoC automatically detects circular dependencies and throws a `PicoIocException` with a clear message:

```csharp
// This will throw: "Circular dependency detected: ServiceA -> ServiceB -> ServiceA"
public class ServiceA(ServiceB b) { }
public class ServiceB(ServiceA a) { }
```

## 📚 Multiple Implementations

Register and resolve multiple implementations of the same interface:

```csharp
container.RegisterSingleton<INotifier>(scope => new EmailNotifier());
container.RegisterSingleton<INotifier>(scope => new SmsNotifier());

using var scope = container.CreateScope();

// Get the last registered implementation
var notifier = scope.GetService<INotifier>(); // SmsNotifier

// Get all implementations
var allNotifiers = scope.GetServices<INotifier>(); // [EmailNotifier, SmsNotifier]
```

## 🏗️ Architecture

```
Pico.IoC/
├── src/
│   ├── Pico.IoC.Abs/     # Abstractions (interfaces, descriptors)
│   ├── Pico.IoC/         # Core implementation
│   └── Pico.IoC.Gen/     # Source Generator
├── samples/              # Sample projects
└── tests/                # Unit tests
```

### How It Works

1. **Compile Time**: The Source Generator scans `Register*<T>()` method calls
2. **Code Generation**: Factory methods are generated with explicit `new` expressions
3. **Runtime**: Services are resolved using the pre-compiled factories (no reflection)

```csharp
// What you write:
container.RegisterSingleton<ILogger, ConsoleLogger>();

// What gets generated:
container.Register(new SvcDescriptor(
    typeof(ILogger),
    static _ => new ConsoleLogger(),
    SvcLifetime.Singleton));
```

## ⚠️ Limitations

- **No Property Injection** - Only constructor injection is supported
- **No Optional Dependencies** - All constructor parameters must be registered
- **No Lazy Resolution** - Services are resolved immediately when requested
- **Source Generator Required** - Type-based registration requires the Source Generator to work

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
