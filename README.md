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
