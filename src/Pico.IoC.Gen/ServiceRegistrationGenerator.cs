namespace Pico.IoC.Gen;

/// <summary>
/// Represents a service registration found in the source code.
/// </summary>
internal record ServiceRegistration(
    string ServiceTypeName,
    string ServiceTypeFullName,
    string ImplementationTypeName,
    string ImplementationTypeFullName,
    string Lifetime, // "Transient", "Scoped", "Singleton"
    bool HasFactory,
    ImmutableArray<ConstructorParameter> ConstructorParameters
);

internal record ConstructorParameter(string TypeFullName, string TypeName, string ParameterName);

/// <summary>
/// Source Generator that scans all ISvcContainer.Register* method calls
/// and generates AOT-compatible factory methods at compile time.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
    private static readonly string[] RegisterMethodNames =
    [
        "Register",
        "RegisterTransient",
        "RegisterScoped",
        "RegisterSingleton"
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all invocations that look like Register* method calls
        var registerInvocations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsRegisterMethodInvocation(node),
                transform: static (ctx, _) => GetInvocationInfo(ctx)
            )
            .Where(static x => x is not null);

        // Combine with compilation and collect all registrations
        var compilationAndInvocations = context.CompilationProvider.Combine(
            registerInvocations.Collect()
        );

        // Generate source
        context.RegisterSourceOutput(
            compilationAndInvocations,
            static (spc, source) => Execute(source.Left, source.Right!, spc)
        );
    }

    /// <summary>
    /// Fast syntax-only check to filter potential Register* method calls.
    /// </summary>
    private static bool IsRegisterMethodInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess
                => GetMethodNameFromMemberAccess(memberAccess),
            _ => null
        };

        return methodName is not null && RegisterMethodNames.Any(m => methodName.StartsWith(m));
    }

    private static string? GetMethodNameFromMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Name switch
        {
            GenericNameSyntax genericName => genericName.Identifier.Text,
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            _ => null
        };
    }

    /// <summary>
    /// Transform invocation syntax to registration info with semantic analysis.
    /// </summary>
    private static InvocationInfo? GetInvocationInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Verify this is a Pico.IoC registration method
        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (
            containingType is null
            || (!containingType.StartsWith("Pico.IoC") && !containingType.Contains("SvcContainer"))
        )
            return null;

        var methodName = methodSymbol.Name;
        if (!RegisterMethodNames.Contains(methodName))
            return null;

        return new InvocationInfo(invocation, semanticModel);
    }

    private record InvocationInfo(
        InvocationExpressionSyntax Invocation,
        SemanticModel SemanticModel
    );

    private static void Execute(
        Compilation compilation,
        ImmutableArray<InvocationInfo?> invocations,
        SourceProductionContext context
    )
    {
        if (invocations.IsDefaultOrEmpty)
            return;

        var registrations = invocations
            .Where(x => x is not null)
            .Select(x => AnalyzeInvocation(x!.Invocation, x.SemanticModel, compilation))
            .OfType<ServiceRegistration>()
            .Distinct()
            .ToList();

        if (registrations.Count == 0)
            return;

        var source = GenerateSource(registrations);
        context.AddSource(
            "PicoIoC.ServiceRegistrations.g.cs",
            SourceText.From(source, Encoding.UTF8)
        );
    }

    private static ServiceRegistration? AnalyzeInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        Compilation compilation
    )
    {
        // Get the method name
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            GenericNameSyntax genericName => genericName.Identifier.Text,
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            _ => null
        };

        if (methodName is null || !RegisterMethodNames.Contains(methodName))
            return null;

        // Check if this is a Pico.IoC registration method
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (
            containingType is null
            || (!containingType.StartsWith("Pico.IoC") && !containingType.Contains("SvcContainer"))
        )
            return null;

        // Extract type arguments
        ITypeSymbol? serviceType = null;
        ITypeSymbol? implementationType = null;
        var lifetime = "Singleton";
        var hasFactory = false;

        // Check for generic type arguments
        var genericNameSyntax = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax gn } => gn,
            GenericNameSyntax gn => gn,
            _ => null
        };

        if (genericNameSyntax?.TypeArgumentList.Arguments.Count > 0)
        {
            var typeArg = genericNameSyntax.TypeArgumentList.Arguments[0];
            serviceType = semanticModel.GetTypeInfo(typeArg).Type;

            if (genericNameSyntax.TypeArgumentList.Arguments.Count > 1)
            {
                var implTypeArg = genericNameSyntax.TypeArgumentList.Arguments[1];
                implementationType = semanticModel.GetTypeInfo(implTypeArg).Type;
            }
            else
            {
                implementationType = serviceType;
            }
        }

        // Check method arguments for factory or lifetime
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var argType = semanticModel.GetTypeInfo(arg.Expression).Type;

            // Check if it's a Func<ISvcScope, T> (factory)
            if (argType is INamedTypeSymbol { Name: "Func" })
            {
                hasFactory = true;
            }
            else
                switch (argType?.Name)
                {
                    // Check if it's SvcLifetime enum
                    case "SvcLifetime":
                        lifetime = arg.Expression.ToString() switch
                        {
                            var s when s.Contains("Transient") => "Transient",
                            var s when s.Contains("Scoped") => "Scoped",
                            _ => "Singleton"
                        };
                        break;
                    // Check if it's a Type argument (for non-generic overloads)
                    case "Type" when arg.Expression is TypeOfExpressionSyntax typeOfExpr:
                    {
                        var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
                        if (serviceType is null)
                            serviceType = typeSymbol;
                        else
                            implementationType = typeSymbol;
                        break;
                    }
                }
        }

        // Infer lifetime from method name if not explicit
        if (methodName.Contains("Transient"))
            lifetime = "Transient";
        else if (methodName.Contains("Scoped"))
            lifetime = "Scoped";
        else if (methodName.Contains("Singleton"))
            lifetime = "Singleton";

        if (serviceType is null || implementationType is null)
            return null;

        // Skip if has factory - we don't need to generate one
        if (hasFactory)
            return null;

        // Skip if implementation type is an interface or abstract class - cannot instantiate
        if (implementationType.TypeKind == TypeKind.Interface || implementationType.IsAbstract)
            return null;

        // Get constructor parameters for the implementation type
        var constructorParams = GetConstructorParameters(implementationType);

        return new ServiceRegistration(
            serviceType.Name,
            serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            implementationType.Name,
            implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            lifetime,
            hasFactory,
            constructorParams
        );
    }

    private static ImmutableArray<ConstructorParameter> GetConstructorParameters(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return ImmutableArray<ConstructorParameter>.Empty;

        // Find the best constructor (prefer the one with most parameters, or [ActivatorUtilitiesConstructor] if present)
        var constructors = namedType
            .Constructors.Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();

        var constructor = constructors.FirstOrDefault();
        if (constructor is null)
            return ImmutableArray<ConstructorParameter>.Empty;

        return
        [
            .. constructor.Parameters.Select(p => new ConstructorParameter(
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                p.Type.Name,
                p.Name
            ))
        ];
    }

    private static string GenerateSource(List<ServiceRegistration> registrations)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Pico.IoC.Gen;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Auto-generated service registrations with AOT-compatible factory methods."
        );
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GeneratedServiceRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the generated service descriptors with pre-compiled factory methods."
        );
        sb.AppendLine(
            "    /// Call <c>container.RegisterRange(GeneratedServiceRegistrations.GetDescriptors())</c> to register all services."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static global::System.Collections.Generic.IEnumerable<global::Pico.IoC.Abs.SvcDescriptor> GetDescriptors()"
        );
        sb.AppendLine("    {");

        foreach (var reg in registrations)
        {
            var lifetimeEnum = $"global::Pico.IoC.Abs.SvcLifetime.{reg.Lifetime}";

            if (reg.ConstructorParameters.IsEmpty)
            {
                // Simple case: parameterless constructor
                sb.AppendLine($"        yield return new global::Pico.IoC.Abs.SvcDescriptor(");
                sb.AppendLine($"            typeof({reg.ServiceTypeFullName}),");
                sb.AppendLine($"            static _ => new {reg.ImplementationTypeFullName}(),");
                sb.AppendLine($"            {lifetimeEnum});");
            }
            else
            {
                // Constructor with dependencies
                sb.AppendLine($"        yield return new global::Pico.IoC.Abs.SvcDescriptor(");
                sb.AppendLine($"            typeof({reg.ServiceTypeFullName}),");
                sb.AppendLine($"            static scope => new {reg.ImplementationTypeFullName}(");

                for (var i = 0; i < reg.ConstructorParameters.Length; i++)
                {
                    var param = reg.ConstructorParameters[i];
                    var comma = i < reg.ConstructorParameters.Length - 1 ? "," : "";
                    sb.AppendLine(
                        $"                ({param.TypeFullName})scope.GetService(typeof({param.TypeFullName})){comma}"
                    );
                }

                sb.AppendLine($"            ),");
                sb.AppendLine($"            {lifetimeEnum});");
            }

            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
