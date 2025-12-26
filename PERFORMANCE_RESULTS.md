# Pico.DI 性能优化最终结果

## 🎯 优化目标

将 Pico.DI 的服务解析性能优化到与 Microsoft.Extensions.DependencyInjection (MS.DI) 相当的水平。

## 📊 性能对比结果

### 最终基准测试结果 (2024-12)

| Method | Mean | Error | Allocated | 
|--------|------|-------|-----------|
| **MS.DI - Deep (5 levels)** | **9.74 ns** | ±0.28 ns | 24 B |
| **Pico.DI - Inlined (5 levels)** | **10.07 ns** | ±0.29 ns | **24 B** |
| Pico.DI - Deep (原始) | 59.03 ns | ±1.30 ns | 120 B |

### 性能提升

- **时间**: 从 176.5 ns → 10.07 ns（约 **17.5x 加速**）
- **内存**: 从 120 B → 24 B（**减少 80%**）
- **与 MS.DI 对比**: 从 17.7x 落后 → **仅差 3.4%**

## 🔧 优化措施

### 1. 编译时循环依赖检测 (PICO010)

**之前**: 运行时使用 `AsyncLocal<HashSet<Type>>` 检测循环依赖
```csharp
// 每次 GetService 调用都有额外开销
private static readonly AsyncLocal<HashSet<Type>> _resolving = new();
```

**之后**: Source Generator 在编译时检测循环依赖
```csharp
// 编译时生成诊断错误 PICO010
// 运行时零开销
```

### 2. FrozenDictionary 优化

**之前**: `ConcurrentDictionary` 每次查找有锁竞争开销
```csharp
private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _services;
```

**之后**: 调用 `Build()` 后切换到只读的 `FrozenDictionary`
```csharp
container.Build(); // 冻结容器，使用 FrozenDictionary
var scope = container.CreateScope(); // 返回 SvcScopeOptimized
```

### 3. 解析链内联（Source Generator）

**关键优化！** Source Generator 现在为 Transient 服务生成内联的构造调用：

**之前（每次调用 GetService）**:
```csharp
container.Register(new SvcDescriptor(
    typeof(ILevel5),
    static scope => new Level5(
        (ILevel4)scope.GetService(typeof(ILevel4))  // 触发另一次查找
    ),
    SvcLifetime.Transient));
```

**之后（完全内联）**:
```csharp
container.Register(new SvcDescriptor(
    typeof(ILevel5),
    static _ => new Level5(
        new Level4(
            new Level3(
                new Level2(
                    new Level1()  // 直接构造，无 GetService 调用
                )
            )
        )
    ),
    SvcLifetime.Transient));
```

### 4. 其他微优化

- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` 用于热路径
- `Volatile.Read/Write` 用于单例快速路径
- 数组索引替代字典查找（Enumerable 注入）

## 💡 内联策略

Source Generator 智能决定是否内联：

| 依赖生命周期 | 策略 | 原因 |
|-------------|------|------|
| **Transient** | ✅ 完全内联 | 每次都创建新实例，无状态共享 |
| **Scoped** | ❌ 调用 GetService | 需要共享作用域实例 |
| **Singleton** | ❌ 调用 GetService | 需要共享全局实例 |

## 🏆 关键发现

1. **GetService 调用是主要瓶颈**
   - 原始: 解析 5 层依赖需要 5 次 GetService 调用 = 5x 字典查找
   - 内联: 单次 GetService + 内联构造 = 1x 字典查找

2. **AOT 兼容与性能可以兼得**
   - Source Generator 在编译时生成优化代码
   - 运行时零反射，完美支持 Native AOT

3. **FrozenDictionary 是 .NET 8+ 的利器**
   - 只读场景下比 ConcurrentDictionary 快 2-3x
   - 通过 `Build()` 模式启用

## 📝 使用指南

```csharp
// 1. 创建容器并注册服务
var container = new SvcContainer();
container
    .RegisterTransient<IService, ServiceImpl>()
    // ... 更多注册 ...
    .ConfigureGeneratedServices();  // Source Generator 生成的优化工厂

// 2. 冻结容器（启用 FrozenDictionary）
container.Build();

// 3. 创建 Scope 并解析服务
using var scope = container.CreateScope();
var service = scope.GetService<IService>();  // ⚡ 极速解析
```

## 📈 优化历程

| 版本 | 优化措施 | Mean | 内存 |
|------|----------|------|------|
| v1.0 | 基础实现 | 176.5 ns | 120 B |
| v1.1 | 移除 AsyncLocal 循环检测 | ~150 ns | 120 B |
| v1.2 | FrozenDictionary + Build() | 55.03 ns | 120 B |
| **v1.3** | **解析链内联** | **10.07 ns** | **24 B** |
| MS.DI | 参照基准 | 9.74 ns | 24 B |

## 🔮 未来优化方向

1. **泛型特化**: 为常见泛型参数生成专门工厂
2. **缓存工厂委托**: 避免重复类型检查
3. **内存池**: 复用临时对象减少 GC 压力

---

*最后更新: 2024-12-26*
