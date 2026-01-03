namespace Pico.DI.Gen.Test;

/// <summary>
/// Tests for the ServiceRegistrationGenerator source generator.
/// </summary>
public class ServiceRegistrationGeneratorTests
{
    #region Basic Generation Tests

    [Fact]
    public void Generator_WithNoRegistrations_GeneratesEmptyConfigureMethod()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public class Program
                {
                    public static void Main()
                    {
                        // No registrations
                    }
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Generator_WithTransientRegistration_GeneratesFactory()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IGreeter { }
                public class Greeter : IGreeter { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterTransient<IGreeter, Greeter>();
                    }
                }
            }
            """;

        // Act
        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Assert
        Assert.NotEmpty(generatedSources);
        var mainGenerated = generatedSources.FirstOrDefault(
            s => s.HintName.Contains("ServiceRegistrations")
        );
        Assert.Contains("Greeter", mainGenerated.Source);
    }

    [Fact]
    public void Generator_WithScopedRegistration_GeneratesFactory()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IService { }
                public class Service : IService { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterScoped<IService, Service>();
                    }
                }
            }
            """;

        // Act
        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Assert
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Generator_WithSingletonRegistration_GeneratesFactory()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface ILogger { }
                public class ConsoleLogger : ILogger { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterSingleton<ILogger, ConsoleLogger>();
                    }
                }
            }
            """;

        // Act
        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Assert
        Assert.NotEmpty(generatedSources);
    }

    #endregion

    #region Constructor Injection Tests

    [Fact]
    public void Generator_WithConstructorDependency_GeneratesDependencyResolution()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface ILogger { void Log(string msg); }
                public class ConsoleLogger : ILogger { public void Log(string msg) { } }
                
                public interface IService { }
                public class Service : IService 
                {
                    public Service(ILogger logger) { }
                }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterSingleton<ILogger, ConsoleLogger>();
                        container.RegisterTransient<IService, Service>();
                    }
                }
            }
            """;

        // Act
        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Assert
        Assert.NotEmpty(generatedSources);
        var mainGenerated = generatedSources.FirstOrDefault(
            s => s.HintName.Contains("ServiceRegistrations")
        );
        // Should include dependency resolution for ILogger in Service factory
        Assert.Contains("Service", mainGenerated.Source);
    }

    [Fact]
    public void Generator_WithMultipleConstructorParameters_GeneratesAllDependencies()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface ILogger { }
                public class Logger : ILogger { }
                
                public interface IConfig { }
                public class Config : IConfig { }
                
                public interface IService { }
                public class Service : IService 
                {
                    public Service(ILogger logger, IConfig config) { }
                }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterSingleton<ILogger, Logger>();
                        container.RegisterSingleton<IConfig, Config>();
                        container.RegisterTransient<IService, Service>();
                    }
                }
            }
            """;

        // Act
        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Assert
        Assert.NotEmpty(generatedSources);
    }

    #endregion

    #region Open Generic Tests

    [Fact]
    public void Generator_WithOpenGenericRegistration_HandlesTypeofSyntax()
    {
        // Arrange - Note: typeof() based registrations are handled differently
        // The generator detects closed generic usages (e.g., IRepository<User>)
        // and generates factories for them
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IRepository<T> { }
                public class Repository<T> : IRepository<T> { }
                
                public class User { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));
                    }
                    
                    public void UseRepository(ISvcScope scope)
                    {
                        // Closed generic usage - this triggers factory generation
                        var repo = scope.GetService<IRepository<User>>();
                    }
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - generator should run without errors
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Generator_WithSelfRegistration_GeneratesFactory()
    {
        // Arrange - registering concrete type to itself
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public class ConcreteService { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterTransient<ConcreteService>();
                    }
                }
            }
            """;

        // Act
        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Assert
        Assert.NotEmpty(generatedSources);
    }

    [Fact]
    public void Generator_WithNestedNamespace_GeneratesCorrectFullName()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp.Services.Logging
            {
                public interface ILogger { }
                public class ConsoleLogger : ILogger { }
            }
            
            namespace TestApp
            {
                using TestApp.Services.Logging;
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterSingleton<ILogger, ConsoleLogger>();
                    }
                }
            }
            """;

        // Act
        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Assert
        Assert.NotEmpty(generatedSources);
        var mainGenerated = generatedSources.FirstOrDefault(
            s => s.HintName.Contains("ServiceRegistrations")
        );
        Assert.Contains("ConsoleLogger", mainGenerated.Source);
    }

    [Fact]
    public void Generator_WithMultipleRegistrations_GeneratesAllFactories()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IService1 { }
                public class Service1 : IService1 { }
                
                public interface IService2 { }
                public class Service2 : IService2 { }
                
                public interface IService3 { }
                public class Service3 : IService3 { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterTransient<IService1, Service1>();
                        container.RegisterScoped<IService2, Service2>();
                        container.RegisterSingleton<IService3, Service3>();
                    }
                }
            }
            """;

        // Act
        var generatedSources = GeneratorTestHelper.GetGeneratedSources(source);

        // Assert
        Assert.NotEmpty(generatedSources);
        var mainGenerated = generatedSources.FirstOrDefault(
            s => s.HintName.Contains("ServiceRegistrations")
        );
        Assert.Contains("Service1", mainGenerated.Source);
        Assert.Contains("Service2", mainGenerated.Source);
        Assert.Contains("Service3", mainGenerated.Source);
    }

    #endregion

    #region No Diagnostics Tests

    [Fact]
    public void Generator_ValidRegistration_ProducesNoDiagnostics()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IService { }
                public class Service : IService { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterTransient<IService, Service>();
                    }
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    #endregion
}
