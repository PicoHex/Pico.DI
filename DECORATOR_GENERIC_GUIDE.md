# Decorator Generic Implementation Guide (AOT Compatible)

## Overview

Pico.DI now supports the **decorator generic pattern**, allowing you to register concrete services (like `IUser -> User`) and then automatically inject generic decorators (like `Logger<IUser>`). All factory generation happens at **compile-time**, ensuring complete AOT compatibility.

## Core Concepts

### 1. What is a Decorator Generic?

A decorator generic is an **open generic type** that can wrap any registered service:

```csharp
// Logger<T> is a decorator generic
public class Logger<T> where T : class
{
    private readonly T _inner;
    
    public Logger(T inner)  // Receives the decorated service
    {
        _inner = inner;
    }
}
```

### 2. Usage Flow

```csharp
// Step 1: Register concrete service
container.RegisterSingleton<IUser, User>();

// Step 2: Register decorator generic (tells source generator Logger<T> can decorate any service)
container.RegisterDecorator<Logger<>>();

// Step 3: Use in code (source generator will detect this call)
var logger = scope.GetService<Logger<IUser>>();  // ✨ Works automatically!

// Source generator will generate equivalent to:
// container.RegisterTransient<Logger<IUser>>(
//     scope => new Logger<IUser>(scope.GetService<IUser>())
// );
```

## Architecture Design

### Hierarchical Structure

```
┌──────────────────────────────────────────┐
│  Usage Layer: GetService<Logger<IUser>>()│
└─────────────────┬────────────────────────┘
                  │
┌─────────────────▼────────────────────────┐
│  Compile-time: Source Code Generator     │
│  ├─ Scan RegisterDecorator<Logger<>>    │
│  ├─ Scan GetService<Logger<IUser>>()    │
│  └─ Generate factory code (zero reflection) │
└─────────────────┬────────────────────────┘
                  │
┌─────────────────▼────────────────────────┐
│  Runtime: Pre-generated factories        │
│  └─ Execute factories only, no reflection│
└──────────────────────────────────────────┘
```

## API Design

### Decorator Registration

```csharp
// Basic form
public ISvcContainer RegisterDecorator<TDecorator>(
    SvcLifetime lifetime = SvcLifetime.Transient,
    int decoratedServiceParameterIndex = 0)
    where TDecorator : class
```

**Parameter Description**:

- `TDecorator`: Decorator generic (like `Logger<>`)
- `lifetime`: Lifetime of decorator instances (usually Transient)
- `decoratedServiceParameterIndex`: Index of parameter receiving the decorated service in constructor

### Decorator Metadata

```csharp
public sealed class DecoratorMetadata
{
    public Type DecoratorType { get; }                          // Logger<>
    public SvcLifetime Lifetime { get; }                        // Transient
    public int DecoratedServiceParameterIndex { get; }          // 0 (first parameter)
}
```

## Implementation Details

### Compile-time Generation Flow

1. **Registration Detection**

   ```csharp
   container.RegisterDecorator<Logger<>>();
   ```

   → Stored to `_decoratorMetadata` dictionary

2. **Usage Detection**

   ```csharp
   var logger = scope.GetService<Logger<IUser>>();
   ```

   → Source generator detects this call

3. **Factory Generation**

   ```csharp
   // Compiler-generated code
   container.Register(new SvcDescriptor(
       typeof(Logger<IUser>),
       scope => new Logger<IUser>(scope.GetService<IUser>()),
       SvcLifetime.Transient
   ));
   ```

4. **Runtime Resolution**
   - Call pre-generated factory
   - Factory constructs `Logger<IUser>` instance
   - Factory calls `scope.GetService<IUser>()` to get decorated service

### Core Advantages

| Aspect | Traditional Reflection DI | Pico.DI Decorator |
|--------|---------------------------|-------------------|
| **Runtime Overhead** | Reflection lookup for decorators | Zero overhead (pre-compiled) |
| **AOT Compatible** | ❌ Not compatible | ✅ Fully compatible |
| **IL Trimming** | ❌ Fragile | ✅ Safe |
| **Compile-time Checking** | ❌ Cannot check | ✅ Can verify |

## Usage Examples

### Basic Example

```csharp
// Define decorator
public class Logger<T> where T : class
{
    private readonly T _inner;
    
    public Logger(T inner)
    {
        _inner = inner;
        Console.WriteLine($"Logger initialized for {typeof(T).Name}");
    }
    
    public T GetService() => _inner;
}

// Usage
container.RegisterSingleton<IUserService, UserService>();
container.RegisterDecorator<Logger<>>();

using var scope = container.CreateScope();
var logger = scope.GetService<Logger<IUserService>>();
logger.GetService().GetUserName();  // Access service through decorator
```

### Complex Decorator (Multiple Dependencies)

```csharp
public class CachingDecorator<T> where T : class
{
    private readonly T _service;
    private readonly ILogger _logger;
    private readonly ICacheProvider _cache;
    
    // Source generator needs to identify which parameter is the decorated service
    public CachingDecorator(T service, ILogger logger, ICacheProvider cache)
    {
        _service = service;
        _logger = logger;
        _cache = cache;
    }
}

// Registration
container.RegisterSingleton<ILogger, ConsoleLogger>();
container.RegisterSingleton<ICacheProvider, InMemoryCache>();
container.RegisterDecorator<CachingDecorator<>>(
    lifetime: SvcLifetime.Transient,
    decoratedServiceParameterIndex: 0  // T is first parameter
);
```

### Nested Decorators

```csharp
// Logger decorates IUserService
container.RegisterTransient<Logger<IUserService>>(
    scope => new Logger<IUserService>(scope.GetService<IUserService>())
);

// Caching decorates Logger<IUserService>
container.RegisterTransient<CachingDecorator<Logger<IUserService>>>(
    scope => new CachingDecorator<Logger<IUserService>>(
        scope.GetService<Logger<IUserService>>()
    )
);

// Use outermost decorator
var cachedLogger = scope.GetService<CachingDecorator<Logger<IUserService>>>();
```

## Source Code Generator Integration

### What the Generator Does

1. **Detect Decorator Registration**

   ```csharp
   // Scan all RegisterDecorator<T>() calls
   // Extract generic type information into registry
   ```

2. **Detect Decorator Usage**

   ```csharp
   // Scan all GetService<Logger<T>>() calls
   // Check if Logger<> is registered as decorator
   ```

3. **Generate Factories**

   ```csharp
   // For each detected Logger<T>, generate factory
   // Factory format: scope => new Logger<T>(scope.GetService<T>())
   ```

4. **Register with Container**

   ```csharp
   // Register all generated factories in ConfigureGeneratedServices()
   ```

### Pseudocode Example

```csharp
// Source generator detects:
container.RegisterDecorator<Logger<>>();
var logger = scope.GetService<Logger<IUserService>>();

// Generates this code in ConfigureGeneratedServices:
public static partial class GeneratedDecoratorFactories
{
    public static void ConfigureGeneratedServices(this ISvcContainer container)
    {
        // Factory for Logger<T>
        container.RegisterTransient<Logger<IUserService>>(
            scope => new Logger<IUserService>(scope.GetService<IUserService>())
        );
        
        // If detects GetService<Logger<IDatabaseService>>, also generate:
        container.RegisterTransient<Logger<IDatabaseService>>(
            scope => new Logger<IDatabaseService>(scope.GetService<IDatabaseService>())
        );
    }
}
```

## Constraints and Limitations

### Current Constraints

1. **Decorator must be generic type**

   ```csharp
   ❌ container.RegisterDecorator<ConcreteLogger>();  // Wrong
   ✅ container.RegisterDecorator<Logger<>>();        // Correct
   ```

2. **Decorator constructor must include decorated service parameter**

   ```csharp
   ✅ public Logger(T service) { }           // Correct
   ❌ public Logger() { }                    // Wrong
   ```

3. **Only closures detected at compile-time work**

   ```csharp
   container.RegisterDecorator<Logger<>>();
   
   // ✅ Source generator detects this, generates factory
   var logger = scope.GetService<Logger<IUser>>();
   
   // ❌ Dynamically constructed types cannot be detected
   var dynamicType = typeof(Logger<>).MakeGenericType(runtimeType);
   var instance = scope.GetService(dynamicType);  // Fails!
   ```

4. **Decorated service must be registered**

   ```csharp
   container.RegisterDecorator<Logger<>>();
   
   // ✅ IUser is registered
   var logger = scope.GetService<Logger<IUser>>();
   
   // ❌ INotRegistered is not registered
   var logger = scope.GetService<Logger<INotRegistered>>();  // Compile-time or runtime error
   ```

## AOT Compatibility Checklist

- [x] No runtime reflection
- [x] All factories generated at compile-time
- [x] IL trimming compatible
- [x] Native AOT compatible
- [x] No dynamic code generation
- [x] No Expression tree compilation
- [x] All types known at compile-time

## Performance Characteristics

| Operation | Cost |
|-----------|------|
| Register decorator | O(1) - dictionary insertion |
| First resolution | O(1) - pre-generated factory lookup |
| Subsequent resolutions | O(1) - cache lookup |
| Memory | Only pre-generated factory code |

## Troubleshooting

### Issue: Decorator cannot be injected

**Cause**: Decorator was not detected by source generator

**Solution**:

1. Ensure explicit `GetService<Logger<T>>()` call in code
2. Ensure `RegisterDecorator<Logger<>>()` has been called
3. Recompile project to run source generator

### Issue: `PicoDiException: Service type not registered`

**Cause**: Decorated service is not registered

**Solution**:

```csharp
// Ensure the decorated service is registered
container.RegisterSingleton<IUser, User>();
container.RegisterDecorator<Logger<>>();
```

### Issue: Decorator constructor signature mismatch

**Cause**: Source generator cannot identify which parameter is the decorated service

**Solution**:

```csharp
// Use decoratedServiceParameterIndex to specify
container.RegisterDecorator<CachingDecorator<>>(
    lifetime: SvcLifetime.Transient,
    decoratedServiceParameterIndex: 0  // T is first parameter
);
```

## Best Practices

1. **Keep decorators simple**

   ```csharp
   // ✅ Good: single responsibility
   public class Logger<T> where T : class
   {
       public Logger(T inner) { /* ... */ }
   }
   
   // ❌ Bad: too many responsibilities
   public class ComplexDecorator<T> where T : class
   {
       public ComplexDecorator(T inner, ILogger logger, ICacheProvider cache, 
                            IMetricsCollector metrics, IAuthService auth) { }
   }
   ```

2. **Use meaningful decorator names**

   ```csharp
   Logger<T>              // ✅ Clear
   ValidationDecorator<T> // ✅ Clear
   X<T>                   // ❌ Unclear
   ```

3. **Document decorators**

   ```csharp
   /// <summary>
   /// Logs all method calls on the wrapped service.
   /// Expects the wrapped service as the first constructor parameter.
   /// </summary>
   public class Logger<T> where T : class { }
   ```

4. **Avoid deep decorator chains**

   ```csharp
   // ✅ Reasonable
   Caching<Logger<IUserService>>
   
   // ❌ Too deep - performance and maintainability issues
   Validation<Caching<Logging<Authorization<IUserService>>>>
   ```

## References

- [SvcContainerDecoratorGenericTests](tests/Pico.DI.Test/SvcContainerDecoratorGenericTests.cs) - Complete test cases
- [DecoratorGenericSample](samples/Pico.DI.Decorator.Sample/DecoratorGenericSample.cs) - Real usage example
- [DecoratorMetadata](src/Pico.DI.Abs/DecoratorMetadata.cs) - Decorator metadata definition
