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

| Method                       | Mean      | Error     | StdDev    | Median    | Rank | Gen0   | Allocated |
|----------------------------- |----------:|----------:|----------:|----------:|-----:|-------:|----------:|
| MS.DI - Singleton            |  6.57 ns  | 0.22 ns   | 0.62 ns   |  6.30 ns  |  1   |   -    |     -     |
| Pico.DI - Singleton          | 10.49 ns  | 0.30 ns   | 0.89 ns   | 10.17 ns  |  2   |   -    |     -     |
| MS.DI - Transient            | 10.56 ns  | 0.35 ns   | 1.04 ns   | 10.25 ns  |  2   | 0.0025 |   24 B    |
| Pico.DI - Transient          | 12.88 ns  | 0.37 ns   | 1.08 ns   | 12.31 ns  |  3   | 0.0025 |   24 B    |
| Pico.DI - Scoped             | 15.35 ns  | 0.35 ns   | 1.00 ns   | 15.02 ns  |  4   |   -    |     -     |
| Pico.DI - Complex (3 deps)   | 16.94 ns  | 0.47 ns   | 1.39 ns   | 16.27 ns  |  5   |   -    |     -     |
| MS.DI - Complex (3 deps)     | 27.20 ns  | 0.63 ns   | 1.83 ns   | 26.47 ns  |  6   |   -    |     -     |
| MS.DI - Scoped               | 30.39 ns  | 0.77 ns   | 2.25 ns   | 29.79 ns  |  7   |   -    |     -     |

| Method                      | Mean     | Error    | StdDev   | Median   | Rank | Gen0   | Allocated |
|---------------------------- |---------:|---------:|---------:|---------:|-----:|-------:|----------:|
| MS.DI - Deep (5 levels)     | 10.47 ns | 0.34 ns  | 0.99 ns  | 10.18 ns |  1   | 0.0025 |   24 B    |
| Pico.DI - Deep (5 levels)   | 19.06 ns | 0.52 ns  | 1.52 ns  | 18.24 ns |  2   | 0.0025 |   24 B    |
```

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
