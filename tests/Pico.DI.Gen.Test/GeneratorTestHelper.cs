using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Pico.DI.Gen.Test;

/// <summary>
/// Helper class for running source generator tests.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Creates a compilation with the given source code.
    /// </summary>
    public static Compilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        // Add System.Runtime reference
        var systemRuntimePath = Path.Combine(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Runtime.dll"
        );
        if (File.Exists(systemRuntimePath))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntimePath));
        }

        // Add netstandard reference if available
        var netstandardPath = Path.Combine(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "netstandard.dll"
        );
        if (File.Exists(netstandardPath))
        {
            references.Add(MetadataReference.CreateFromFile(netstandardPath));
        }

        // Add Pico.DI.Abs reference
        references.Add(
            MetadataReference.CreateFromFile(typeof(Pico.DI.Abs.ISvcContainer).Assembly.Location)
        );

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    /// <summary>
    /// Runs the source generator on the given source code and returns the result.
    /// </summary>
    public static GeneratorDriverRunResult RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new ServiceRegistrationGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );

        return driver.GetRunResult();
    }

    /// <summary>
    /// Runs the source generator and returns the generated source texts.
    /// </summary>
    public static ImmutableArray<(string HintName, string Source)> GetGeneratedSources(
        string source
    )
    {
        var result = RunGenerator(source);
        return result
            .GeneratedTrees
            .Select(t => (Path.GetFileName(t.FilePath), t.GetText().ToString()))
            .ToImmutableArray();
    }

    /// <summary>
    /// Runs the analyzer on the given source and returns diagnostics.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source)
    {
        var compilation = CreateCompilation(source);
        var analyzer = new ServiceRegistrationAnalyzer();

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer)
        );

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
