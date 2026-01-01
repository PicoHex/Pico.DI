# Pico.DI.TUnit.Test

TUnit 单元测试项目，用于测试 Pico.DI 依赖注入容器。

## 运行测试

### 基本运行
```bash
# 使用 dotnet test（推荐）
dotnet test --project tests/Pico.DI.TUnit.Test/Pico.DI.TUnit.Test.csproj

# 或使用 dotnet run
cd tests/Pico.DI.TUnit.Test
dotnet run
```

### 带覆盖率运行
```bash
# 使用 dotnet test
dotnet test --project tests/Pico.DI.TUnit.Test/Pico.DI.TUnit.Test.csproj --configuration Release --coverage

# 使用 dotnet run 并指定输出格式
cd tests/Pico.DI.TUnit.Test
dotnet run --configuration Release --coverage --coverage-output-format cobertura
```

### 使用脚本运行（含覆盖率摘要）
```powershell
cd tests/Pico.DI.TUnit.Test
.\run-coverage.ps1

# 生成 HTML 报告
.\run-coverage.ps1 -GenerateHtmlReport
```

## 测试覆盖范围

本测试项目覆盖以下主要功能：

| 测试文件 | 覆盖功能 |
|----------|----------|
| `SvcContainerRegistrationTests.cs` | 服务注册（类型、实例、工厂） |
| `SvcContainerFactoryRegistrationTests.cs` | 工厂方法注册 |
| `SvcScopeLifetimeTests.cs` | 服务生命周期（Transient/Scoped/Singleton） |
| `SvcContainerDisposeTests.cs` | 资源释放 |
| `SvcContainerErrorTests.cs` | 错误处理和异常 |
| `SvcDescriptorTests.cs` | 服务描述符 |
| `SvcContainerOpenGenericTests.cs` | 开放泛型支持 |
| `SvcContainerEnumerableInjectionTests.cs` | 可枚举注入 |
| `SvcContainerDecoratorTests.cs` | 装饰器模式 |
| `SvcLifetimeTests.cs` | 生命周期枚举 |
| `SvcScopeNestedScopeTests.cs` | 嵌套作用域 |
| `ISvcScopeTests.cs` | 作用域接口 |
| `SvcContainerConcurrencyTests.cs` | 并发测试 |
| `SvcContainerIntegrationTests.cs` | 集成测试 |

## 依赖

- [TUnit](https://github.com/thomhurst/TUnit) v1.7.7 - 现代 .NET 测试框架
- 内置 Microsoft.Testing.Extensions.CodeCoverage - 代码覆盖率支持

## 注意事项

- 本项目使用 TUnit 测试框架，**不兼容 Coverlet**
- 覆盖率由 `Microsoft.Testing.Extensions.CodeCoverage` 提供（TUnit 元包自带）
- 需要 .NET 10.0 SDK
- 项目根目录的 `global.json` 配置了使用 Microsoft.Testing.Platform 模式
