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
        // Register the attribute source
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource(
                "GenerateServiceRegistrationsAttribute.g.cs",
                SourceText.From(
                    """
                    namespace Pico.IoC.Gen;

                    /// <summary>
                    /// Marks a class that uses Pico.IoC registration methods to have its service descriptors generated at compile time.
                    /// Apply this attribute to a partial class that contains service registration calls.
                    /// </summary>
                    [global::System.AttributeUsage(global::System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                    internal sealed class GenerateServiceRegistrationsAttribute : global::System.Attribute;
                    """,
                    Encoding.UTF8
                )
            );
        });

        // Find classes with the attribute
        var classesWithAttribute = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                "Pico.IoC.Gen.GenerateServiceRegistrationsAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetClassInfo(ctx)
            )
            .Where(static x => x is not null);

        // Combine with compilation
        var compilationAndClasses = context.CompilationProvider.Combine(
            classesWithAttribute.Collect()
        );

        // Generate source
        context.RegisterSourceOutput(
            compilationAndClasses,
            static (spc, source) => Execute(source.Left, source.Right!, spc)
        );
    }

    private static ClassInfo? GetClassInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetNode is not ClassDeclarationSyntax classDecl)
            return null;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null)
            return null;

        return new ClassInfo(
            classDecl,
            classSymbol.ContainingNamespace.ToDisplayString(),
            classSymbol.Name,
            classSymbol.IsStatic
        );
    }

    private record ClassInfo(
        ClassDeclarationSyntax ClassDeclaration,
        string Namespace,
        string ClassName,
        bool IsStatic
    );

    private static void Execute(
        Compilation compilation,
        ImmutableArray<ClassInfo?> classes,
        SourceProductionContext context
    )
    {
        if (classes.IsDefaultOrEmpty)
            return;

        foreach (var classInfo in classes)
        {
            if (classInfo is null)
                continue;

            var registrations = FindRegistrations(compilation, classInfo.ClassDeclaration, context);
            if (registrations.Count == 0)
                continue;

            var source = GenerateSource(classInfo, registrations);
            context.AddSource(
                $"{classInfo.ClassName}.ServiceRegistrations.g.cs",
                SourceText.From(source, Encoding.UTF8)
            );
        }
    }

    private static List<ServiceRegistration> FindRegistrations(
        Compilation compilation,
        ClassDeclarationSyntax classDecl,
        SourceProductionContext context
    )
    {
        var registrations = new List<ServiceRegistration>();
        var semanticModel = compilation.GetSemanticModel(classDecl.SyntaxTree);

        // Find all method invocations in the class
        var invocations = classDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var registration = AnalyzeInvocation(invocation, semanticModel, compilation);
            if (registration is not null)
            {
                registrations.Add(registration);
            }
        }

        return registrations;
    }

    private static ServiceRegistration? AnalyzeInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        Compilation compilation
    )
    {
        // Get the method name
        string? methodName = invocation.Expression switch
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
        string lifetime = "Singleton";
        bool hasFactory = false;

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
            // Check if it's SvcLifetime enum
            else if (argType?.Name == "SvcLifetime")
            {
                lifetime = arg.Expression.ToString() switch
                {
                    var s when s.Contains("Transient") => "Transient",
                    var s when s.Contains("Scoped") => "Scoped",
                    _ => "Singleton"
                };
            }
            // Check if it's a Type argument (for non-generic overloads)
            else if (argType?.Name == "Type" && arg.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
                if (serviceType is null)
                    serviceType = typeSymbol;
                else
                    implementationType = typeSymbol;
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

        return constructor
            .Parameters.Select(p => new ConstructorParameter(
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                p.Type.Name,
                p.Name
            ))
            .ToImmutableArray();
    }

    private static string GenerateSource(
        ClassInfo classInfo,
        List<ServiceRegistration> registrations
    )
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (
            !string.IsNullOrEmpty(classInfo.Namespace)
            && classInfo.Namespace != "<global namespace>"
        )
        {
            sb.AppendLine($"namespace {classInfo.Namespace};");
            sb.AppendLine();
        }

        var staticModifier = classInfo.IsStatic ? "static " : "";
        sb.AppendLine($"{staticModifier}partial class {classInfo.ClassName}");
        sb.AppendLine("{");

        // Generate a method that creates all the factories
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the generated service descriptors with pre-compiled factory methods."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static global::System.Collections.Generic.IEnumerable<global::Pico.IoC.Abs.SvcDescriptor> GetGeneratedDescriptors()"
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

                for (int i = 0; i < reg.ConstructorParameters.Length; i++)
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
