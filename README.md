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
Native AOT Benchmark, Windows 11, 12th Gen Intel Core i7-1260P
.NET 10.0.1, Native AOT compiled, 67M iterations

╔════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╗
║                SERVICE RESOLUTION (Deep Dependency, 5 levels) — Lower is better (ns/op)                                                 ║
╠════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣
║ Method                        | Mean (ns) | Allocated | Notes                                                                            ║
╠═══════════════════════════════╬═══════════╬═══════════╬================================================================================╣
║ MS.DI - Deep (5 levels)       | 10.99     |   24 B    | Reference: Microsoft.Extensions.DependencyInjection                              ║
║ Pico.DI - Inlined (5 levels)  | 11.15     |   24 B    | 🔥 Inlined chain, zero-reflection, matches MS.DI                                 ║
║ Pico.DI - Deep (5 levels)     | 57.60     |  120 B    | (Old) Chained GetService, no inlining                                            ║
╚═══════════════════════════════╩═══════════╩═══════════╩══════════════════════════════════════════════════════════════════════════════════╝
```

**📊 Analysis:**

- **Pico.DI (inlined):** Now matches MS.DI in deep dependency resolution performance (5-level chain, ~11ns/op, 24B alloc)
- **Old Pico.DI (chained):** ~5x slower due to repeated GetService calls
- **Key Advantage:** Pico.DI provides **true zero-reflection**, **compile-time cycle detection**, and **AOT safety**
- **Binary Size:** ~2.1 MB AOT benchmark app (includes both DI frameworks + test harness)

*Run AOT benchmark: `cd benchmarks/Pico.DI.AotBenchmark && dotnet publish -c Release -r win-x64 && bin\Release\net10.0\win-x64\publish\Pico.DI.AotBenchmark.exe`*

---

## 📦 Install

### Option A: CLI

```bash
dotnet add package Pico.DI
dotnet add package Pico.DI.Gen
```

### Option B: csproj

```xml
<ItemGroup>
  <PackageReference Include="Pico.DI" Version="1.0.0" />
  <PackageReference Include="Pico.DI.Gen" Version="1.0.0" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### Option C: Just Abstractions (for library authors)

```bash
dotnet add package Pico.DI.Abs
```

### Packages

| Package | Size | Purpose |
|---------|------|---------|
| `Pico.DI` | ~13KB | Runtime container |
| `Pico.DI.Abs` | ~12KB | Interfaces only |
| `Pico.DI.Gen` | ~25KB | Source generator (compile-time) |

---

## 🎯 Platforms

```
✅ Supported                          ❌ Not Supported
─────────────────────────────────────────────────────
☁️  Cloud / Microservices             🔌 Arduino
🖥️  Desktop (Win/Mac/Linux)           📟 ESP32  
🐳 Docker / Kubernetes               🎮 Bare-metal MCU
🥧 Raspberry Pi (ARM64)              
🤖 NVIDIA Jetson                     
🏭 Industrial Gateways               
📱 Windows IoT                       
⚡ Serverless (Lambda, Functions)    
```

### Build for Your Target

```bash
# 🐧 Linux x64
dotnet publish -r linux-x64 -c Release -p:PublishAot=true

# 🥧 Raspberry Pi
dotnet publish -r linux-arm64 -c Release -p:PublishAot=true

# 🪟 Windows
dotnet publish -r win-x64 -c Release -p:PublishAot=true

# 🍎 macOS
dotnet publish -r osx-arm64 -c Release -p:PublishAot=true
```

### AOT Config

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <!-- OR trimming only: -->
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>full</TrimMode>
</PropertyGroup>
```

**Binary size:** ~150KB (trimmed, self-contained sample app)

---

## 📚 Docs

### Lifetimes

```csharp
container
    .RegisterTransient<IFoo, Foo>()    // 🔄 New instance every time
    .RegisterScoped<IBar, Bar>()       // 📦 One per scope
    .RegisterSingleton<IBaz, Baz>();   // 🌍 One for app lifetime
```

### Registration Styles

```csharp
// 1️⃣ Type mapping (AOT-safe, source-generated)
container.RegisterScoped<IService, ServiceImpl>();

// 2️⃣ Factory delegate
container.RegisterScoped<IDb>(s => new Db(connectionString));

// 3️⃣ Instance
container.RegisterSingle<IConfig>(new Config { Env = "prod" });

// 4️⃣ Open generics
container.RegisterScoped(typeof(IRepo<>), typeof(Repo<>));
```

### Multiple Implementations

```csharp
container
    .RegisterSingleton<IHandler>(s => new EmailHandler())
    .RegisterSingleton<IHandler>(s => new SmsHandler())
    .RegisterSingleton<IHandler>(s => new PushHandler());

// Get all
var handlers = scope.GetServices<IHandler>();
```

### Compile-Time Diagnostics

```
⚠️ PICO001: Service 'IFoo' is not registered
⚠️ PICO002: Circular dependency: A → B → A  
❌ PICO003: Cannot use abstract 'Foo' as implementation
❌ PICO004: 'Bar' has no public constructor
```

---

## 🔧 Internals

### How It Works

```
┌──────────────────┐      ┌──────────────────┐
│   Your Code      │      │  Generated Code  │
├──────────────────┤  =>  ├──────────────────┤
│ .RegisterScoped  │      │ new SvcDescriptor│
│   <IFoo, Foo>()  │      │   (typeof(IFoo), │
│                  │      │    _ => new Foo()│
│                  │      │    Scoped)       │
└──────────────────┘      └──────────────────┘
       ↓ Roslyn Source Generator (compile-time)
```

### Architecture

```
┌─────────────────────────────────────────────┐
│              Pico.DI.Gen                    │
│  ┌────────────────────────────────────────┐ │
│  │ Source Generator + Roslyn Analyzer     │ │
│  │ • Scans Register<T>() calls            │ │
│  │ • Emits factory code                   │ │
│  │ • Zero runtime cost                    │ │
│  └────────────────────────────────────────┘ │
├─────────────────────────────────────────────┤
│              Pico.DI                        │
│  ┌────────────────────────────────────────┐ │
│  │ SvcContainer + SvcScope                │ │
│  │ • Lifetime management                  │ │
│  │ • Service resolution                   │ │
│  │ • Disposal handling                    │ │
│  └────────────────────────────────────────┘ │
├─────────────────────────────────────────────┤
│              Pico.DI.Abs                    │
│  ┌────────────────────────────────────────┐ │
│  │ ISvcContainer, ISvcScope               │ │
│  │ SvcDescriptor, SvcLifetime             │ │
│  └────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

---

## ⚠️ Limitations

```
✅ Constructor injection      ❌ Property injection
✅ IServiceProvider adapter   ❌ Optional parameters  
✅ Async disposal             ❌ Lazy<T> resolution
✅ .NET 10+                   ❌ .NET 8/9 (needs C# 14)
```

---

## 🤝 Contributing

```bash
git clone https://github.com/pico-di/Pico.DI
cd Pico.DI
dotnet test
```

PRs welcome. Keep it minimal.

---

## 📄 License

MIT — Use it however you want.

---

<div align="center">

```
     _____
    /     \     "The best DI is the one you 
   | () () |     don't notice at runtime."
    \  ^  /     
     |||||              - Ancient Geek Proverb
     |||||
```

**Cloud • Edge • Embedded • Everywhere .NET runs**

</div>
