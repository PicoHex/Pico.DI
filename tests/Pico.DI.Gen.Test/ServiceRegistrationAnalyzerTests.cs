namespace Pico.DI.Gen.Test;

/// <summary>
/// Tests for the ServiceRegistrationAnalyzer diagnostic analyzer.
/// </summary>
public class ServiceRegistrationAnalyzerTests
{
    #region Abstract Type Registration Tests

    [Fact]
    public async Task Analyzer_AbstractTypeAsImplementation_ReportsDiagnostic()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IService { }
                public abstract class AbstractService : IService { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterTransient<IService, AbstractService>();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        var abstractTypeDiagnostic = diagnostics.FirstOrDefault(
            d => d.Id == ServiceRegistrationAnalyzer.AbstractTypeRegistrationId
        );
        Assert.NotNull(abstractTypeDiagnostic);
    }

    [Fact]
    public async Task Analyzer_InterfaceAsImplementation_ReportsDiagnostic()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IService { }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        // Trying to register interface as its own implementation
                        container.RegisterTransient<IService, IService>();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync(source);

        // Assert - should report abstract type registration error
        var abstractTypeDiagnostic = diagnostics.FirstOrDefault(
            d => d.Id == ServiceRegistrationAnalyzer.AbstractTypeRegistrationId
        );
        Assert.NotNull(abstractTypeDiagnostic);
    }

    #endregion

    #region Missing Public Constructor Tests

    [Fact]
    public async Task Analyzer_TypeWithNoPublicConstructor_ReportsDiagnostic()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IService { }
                public class Service : IService 
                {
                    private Service() { }
                }
                
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
        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync(source);

        // Assert
        var missingConstructorDiagnostic = diagnostics.FirstOrDefault(
            d => d.Id == ServiceRegistrationAnalyzer.MissingPublicConstructorId
        );
        Assert.NotNull(missingConstructorDiagnostic);
    }

    #endregion

    #region Valid Registration Tests (No Diagnostics)

    [Fact]
    public async Task Analyzer_ValidConcreteTypeRegistration_NoDiagnostics()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface IService { }
                public class Service : IService 
                {
                    public Service() { }
                }
                
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
        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync(source);

        // Assert - should have no errors
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Analyzer_ValidRegistrationWithDependencies_NoDiagnostics()
    {
        // Arrange
        var source = """
            using Pico.DI.Abs;
            
            namespace TestApp
            {
                public interface ILogger { }
                public class Logger : ILogger { }
                
                public interface IService { }
                public class Service : IService 
                {
                    public Service(ILogger logger) { }
                }
                
                public class Startup
                {
                    public void Configure(ISvcContainer container)
                    {
                        container.RegisterSingleton<ILogger, Logger>();
                        container.RegisterTransient<IService, Service>();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync(source);

        // Assert - should have no errors (unregistered dependency is a warning)
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    #endregion

    #region Unregistered Dependency Warning Tests

    [Fact]
    public async Task Analyzer_UnregisteredDependency_RuleIsDefined()
    {
        // The UnregisteredDependency rule is defined but not currently triggered by the analyzer
        // This test verifies the rule exists in the supported diagnostics
        var analyzer = new ServiceRegistrationAnalyzer();
        var supportedDiagnostics = analyzer.SupportedDiagnostics;

        var unregisteredRule = supportedDiagnostics.FirstOrDefault(
            d => d.Id == ServiceRegistrationAnalyzer.UnregisteredDependencyId
        );

        Assert.NotNull(unregisteredRule);
        Assert.Equal(DiagnosticSeverity.Warning, unregisteredRule.DefaultSeverity);
        await Task.CompletedTask; // Keep async signature for consistency
    }

    #endregion

    #region Diagnostic ID Constants Tests

    [Fact]
    public void Analyzer_DiagnosticIds_AreCorrectFormat()
    {
        // Assert diagnostic IDs follow the expected format
        Assert.Equal("PICO001", ServiceRegistrationAnalyzer.UnregisteredDependencyId);
        Assert.Equal("PICO002", ServiceRegistrationAnalyzer.CircularDependencyId);
        Assert.Equal("PICO003", ServiceRegistrationAnalyzer.AbstractTypeRegistrationId);
        Assert.Equal("PICO004", ServiceRegistrationAnalyzer.MissingPublicConstructorId);
    }

    [Fact]
    public void Analyzer_SupportedDiagnostics_ContainsAllRules()
    {
        // Arrange
        var analyzer = new ServiceRegistrationAnalyzer();

        // Act
        var supportedDiagnostics = analyzer.SupportedDiagnostics;

        // Assert
        Assert.Equal(4, supportedDiagnostics.Length);
        Assert.Contains(
            supportedDiagnostics,
            d => d.Id == ServiceRegistrationAnalyzer.UnregisteredDependencyId
        );
        Assert.Contains(
            supportedDiagnostics,
            d => d.Id == ServiceRegistrationAnalyzer.CircularDependencyId
        );
        Assert.Contains(
            supportedDiagnostics,
            d => d.Id == ServiceRegistrationAnalyzer.AbstractTypeRegistrationId
        );
        Assert.Contains(
            supportedDiagnostics,
            d => d.Id == ServiceRegistrationAnalyzer.MissingPublicConstructorId
        );
    }

    #endregion
}
