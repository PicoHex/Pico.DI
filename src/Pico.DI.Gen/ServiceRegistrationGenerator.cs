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

/// <summary>
/// Represents an open generic registration (e.g., IRepository&lt;&gt; -&gt; Repository&lt;&gt;).
/// </summary>
internal record OpenGenericRegistration(
    string OpenServiceTypeFullName, // e.g., "IRepository<>"
    string OpenImplementationTypeFullName, // e.g., "Repository<>"
    string Lifetime,
    int TypeParameterCount
);

/// <summary>
/// Represents a closed generic type usage that needs to be pre-generated for AOT.
/// </summary>
internal record ClosedGenericUsage(
    string ClosedServiceTypeFullName, // e.g., "IRepository<User>"
    string OpenServiceTypeFullName, // e.g., "IRepository<>"
    ImmutableArray<string> TypeArgumentsFullNames // e.g., ["User"]
);

internal record ConstructorParameter(string TypeFullName, string TypeName, string ParameterName);

/// <summary>
/// Source Generator that scans all ISvcContainer.Register* method calls
/// and generates AOT-compatible factory methods at compile time.
/// Also handles open generic registrations by detecting closed generic usages.
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

    private static readonly string[] OpenGenericRegisterMethodNames =
    [
        "RegisterOpenGeneric",
        "RegisterOpenGenericTransient",
        "RegisterOpenGenericScoped",
        "RegisterOpenGenericSingleton"
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all invocations that look like Register* method calls (regular registrations)
        var registerInvocations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsRegisterMethodInvocation(node),
                transform: static (ctx, _) => GetInvocationInfo(ctx)
            )
            .Where(static x => x is not null);

        // Find all open generic registrations (RegisterOpenGeneric*)
        var openGenericRegistrations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsOpenGenericRegisterInvocation(node),
                transform: static (ctx, _) => GetOpenGenericInvocationInfo(ctx)
            )
            .Where(static x => x is not null);

        // Find all GetService<T> calls to detect closed generic usages
        var closedGenericUsages = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsGetServiceInvocation(node),
                transform: static (ctx, _) => GetClosedGenericUsageInfo(ctx)
            )
            .Where(static x => x is not null);

        // Combine all sources with compilation
        var compilationAndInvocations = context
            .CompilationProvider.Combine(registerInvocations.Collect())
            .Combine(openGenericRegistrations.Collect())
            .Combine(closedGenericUsages.Collect());

        // Generate source
        context.RegisterSourceOutput(
            compilationAndInvocations,
            static (spc, source) =>
            {
                var ((compilationAndRegs, openGenerics), closedUsages) = source;
                var (compilation, invocations) = compilationAndRegs;
                Execute(compilation, invocations, openGenerics!, closedUsages!, spc);
            }
        );
    }

    /// <summary>
    /// Check if this is a GetService&lt;T&gt; invocation.
    /// </summary>
    private static bool IsGetServiceInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess
                => GetMethodNameFromMemberAccess(memberAccess),
            _ => null
        };

        return methodName is "GetService" or "GetServices";
    }

    /// <summary>
    /// Check if this is a RegisterOpenGeneric* invocation.
    /// </summary>
    private static bool IsOpenGenericRegisterInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess
                => GetMethodNameFromMemberAccess(memberAccess),
            _ => null
        };

        return methodName is not null
            && OpenGenericRegisterMethodNames.Any(m => methodName.StartsWith(m));
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

        // Exclude open generic methods - they're handled separately
        if (
            methodName is not null
            && OpenGenericRegisterMethodNames.Any(m => methodName.StartsWith(m))
        )
            return false;

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

        // Verify this is a Pico.DI registration method
        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (
            containingType is null
            || (!containingType.StartsWith("Pico.DI") && !containingType.Contains("SvcContainer"))
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

    private record OpenGenericInvocationInfo(
        InvocationExpressionSyntax Invocation,
        SemanticModel SemanticModel
    );

    private record ClosedGenericUsageInfo(
        InvocationExpressionSyntax Invocation,
        SemanticModel SemanticModel,
        ITypeSymbol ClosedGenericType
    );

    /// <summary>
    /// Extract open generic registration info.
    /// </summary>
    private static OpenGenericInvocationInfo? GetOpenGenericInvocationInfo(
        GeneratorSyntaxContext context
    )
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Verify this is a Pico.DI registration method
        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (
            containingType is null
            || (!containingType.StartsWith("Pico.DI") && !containingType.Contains("SvcContainer"))
        )
            return null;

        var methodName = methodSymbol.Name;
        if (!OpenGenericRegisterMethodNames.Contains(methodName))
            return null;

        return new OpenGenericInvocationInfo(invocation, semanticModel);
    }

    /// <summary>
    /// Extract closed generic usage from GetService&lt;T&gt; calls.
    /// </summary>
    private static ClosedGenericUsageInfo? GetClosedGenericUsageInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Check if it's a GetService<T> or GetServices<T> method
        if (methodSymbol.Name is not ("GetService" or "GetServices"))
            return null;

        // Must be generic with type argument
        if (!methodSymbol.IsGenericMethod || methodSymbol.TypeArguments.Length == 0)
            return null;

        var typeArg = methodSymbol.TypeArguments[0];

        // Only interested in closed generic types
        if (
            typeArg
            is not INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } namedType
        )
            return null;

        // Skip if it's IEnumerable<T> or other system types
        var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("System"))
            return null;

        return new ClosedGenericUsageInfo(invocation, semanticModel, namedType);
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<InvocationInfo?> invocations,
        ImmutableArray<OpenGenericInvocationInfo?> openGenericInvocations,
        ImmutableArray<ClosedGenericUsageInfo?> closedGenericUsages,
        SourceProductionContext context
    )
    {
        // Process regular registrations
        var registrations = invocations
            .Where(x => x is not null)
            .Select(x => AnalyzeInvocation(x!.Invocation, x.SemanticModel, compilation))
            .OfType<ServiceRegistration>()
            .Distinct()
            .ToList();

        // Process open generic registrations
        var openGenerics = openGenericInvocations
            .Where(x => x is not null)
            .Select(x => AnalyzeOpenGenericInvocation(x!.Invocation, x.SemanticModel))
            .OfType<OpenGenericRegistration>()
            .Distinct()
            .ToList();

        // Process closed generic usages
        var closedUsages = closedGenericUsages
            .Where(x => x is not null)
            .Select(x => AnalyzeClosedGenericUsage(x!.ClosedGenericType))
            .OfType<ClosedGenericUsage>()
            .Distinct()
            .ToList();

        // Generate closed generic registrations from open generic + usages
        var generatedClosedGenerics = GenerateClosedGenericRegistrations(
            openGenerics,
            closedUsages,
            compilation
        );

        // Combine all registrations
        var allRegistrations = registrations.Concat(generatedClosedGenerics).Distinct().ToList();

        if (allRegistrations.Count == 0)
            return;

        var source = GenerateSource(allRegistrations);
        context.AddSource(
            "PicoIoC.ServiceRegistrations.g.cs",
            SourceText.From(source, Encoding.UTF8)
        );
    }

    /// <summary>
    /// Analyze an open generic registration call.
    /// </summary>
    private static OpenGenericRegistration? AnalyzeOpenGenericInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel
    )
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        if (methodName is null || !OpenGenericRegisterMethodNames.Contains(methodName))
            return null;

        // Parse arguments: typically (typeof(IRepository<>), typeof(Repository<>), SvcLifetime.X)
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 2)
            return null;

        ITypeSymbol? serviceType = null;
        ITypeSymbol? implementationType = null;
        var lifetime = "Singleton";

        foreach (var arg in args)
        {
            if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
                if (typeSymbol is INamedTypeSymbol { IsUnboundGenericType: true } unboundType)
                {
                    if (serviceType is null)
                        serviceType = unboundType;
                    else
                        implementationType = unboundType;
                }
            }
            else
            {
                var argType = semanticModel.GetTypeInfo(arg.Expression).Type;
                if (argType?.Name == "SvcLifetime")
                {
                    lifetime = arg.Expression.ToString() switch
                    {
                        var s when s.Contains("Transient") => "Transient",
                        var s when s.Contains("Scoped") => "Scoped",
                        _ => "Singleton"
                    };
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

        if (
            serviceType is not INamedTypeSymbol namedServiceType
            || implementationType is not INamedTypeSymbol namedImplType
        )
            return null;

        return new OpenGenericRegistration(
            namedServiceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            namedImplType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            lifetime,
            namedServiceType.TypeParameters.Length
        );
    }

    /// <summary>
    /// Analyze a closed generic usage.
    /// </summary>
    private static ClosedGenericUsage? AnalyzeClosedGenericUsage(ITypeSymbol closedType)
    {
        if (closedType is not INamedTypeSymbol { IsGenericType: true } namedType)
            return null;

        var openType = namedType.ConstructUnboundGenericType();
        var typeArgs = namedType
            .TypeArguments.Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToImmutableArray();

        return new ClosedGenericUsage(
            namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            openType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            typeArgs
        );
    }

    /// <summary>
    /// Generate ServiceRegistration entries for closed generic types based on
    /// open generic registrations and detected usages.
    /// </summary>
    private static List<ServiceRegistration> GenerateClosedGenericRegistrations(
        List<OpenGenericRegistration> openGenerics,
        List<ClosedGenericUsage> closedUsages,
        Compilation compilation
    )
    {
        var result = new List<ServiceRegistration>();

        foreach (var usage in closedUsages)
        {
            // Find matching open generic registration
            var openGeneric = openGenerics.FirstOrDefault(og =>
                og.OpenServiceTypeFullName == usage.OpenServiceTypeFullName
            );

            if (openGeneric is null)
                continue;

            // Build closed implementation type by applying type arguments
            var closedImplTypeFullName = BuildClosedGenericTypeName(
                openGeneric.OpenImplementationTypeFullName,
                usage.TypeArgumentsFullNames
            );

            // Try to get the actual closed implementation type symbol
            var closedImplType = GetTypeByMetadataName(compilation, closedImplTypeFullName);

            var constructorParams = closedImplType is not null
                ? GetConstructorParameters(closedImplType)
                : ImmutableArray<ConstructorParameter>.Empty;

            result.Add(
                new ServiceRegistration(
                    GetSimpleName(usage.ClosedServiceTypeFullName),
                    usage.ClosedServiceTypeFullName,
                    GetSimpleName(closedImplTypeFullName),
                    closedImplTypeFullName,
                    openGeneric.Lifetime,
                    false,
                    constructorParams
                )
            );
        }

        return result;
    }

    /// <summary>
    /// Build a closed generic type name from an open generic type and type arguments.
    /// E.g., "global::Ns.Repository&lt;&gt;" + ["global::Ns.User"] -> "global::Ns.Repository&lt;global::Ns.User&gt;"
    /// </summary>
    private static string BuildClosedGenericTypeName(
        string openTypeFullName,
        ImmutableArray<string> typeArguments
    )
    {
        // Handle format like "global::Namespace.Type<>" or "global::Namespace.Type<,>"
        var angleBracketIndex = openTypeFullName.IndexOf('<');
        if (angleBracketIndex < 0)
            return openTypeFullName;

        var baseName = openTypeFullName.Substring(0, angleBracketIndex);
        var typeArgsStr = string.Join(", ", typeArguments);

        return $"{baseName}<{typeArgsStr}>";
    }

    /// <summary>
    /// Get simple type name from full name.
    /// </summary>
    private static string GetSimpleName(string fullName)
    {
        // Remove "global::" prefix and get last part
        var name = fullName.Replace("global::", "");
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
            name = name.Substring(lastDot + 1);

        // Clean up generic markers
        var angleIndex = name.IndexOf('<');
        if (angleIndex >= 0)
            name = name.Substring(0, angleIndex);

        return name;
    }

    /// <summary>
    /// Try to get a type symbol by its full name including generic arguments.
    /// </summary>
    private static INamedTypeSymbol? GetTypeByMetadataName(Compilation compilation, string fullName)
    {
        // For closed generic types, we need to construct them from the open generic
        // Parse out the base type and type arguments
        var angleBracketIndex = fullName.IndexOf('<');
        if (angleBracketIndex < 0)
        {
            var cleanName = fullName.Replace("global::", "");
            return compilation.GetTypeByMetadataName(cleanName);
        }

        var baseName = fullName.Substring(0, angleBracketIndex).Replace("global::", "");
        var typeArgsStr = fullName.Substring(
            angleBracketIndex + 1,
            fullName.Length - angleBracketIndex - 2
        );

        // Parse type arguments (simple split by ", " - may need more robust parsing for nested generics)
        var typeArgNames = ParseTypeArguments(typeArgsStr);

        // Get arity for open generic lookup
        var arity = typeArgNames.Count;
        var metadataName = $"{baseName}`{arity}";

        var openType = compilation.GetTypeByMetadataName(metadataName);
        if (openType is null)
            return null;

        // Get type argument symbols
        var typeArgSymbols = new List<ITypeSymbol>();
        foreach (var argName in typeArgNames)
        {
            var argType = GetTypeByMetadataName(compilation, argName);
            if (argType is null)
                return null;
            typeArgSymbols.Add(argType);
        }

        return openType.Construct(typeArgSymbols.ToArray());
    }

    /// <summary>
    /// Parse type arguments from a string like "global::Ns.Type1, global::Ns.Type2".
    /// Handles nested generics.
    /// </summary>
    private static List<string> ParseTypeArguments(string typeArgsStr)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < typeArgsStr.Length; i++)
        {
            var c = typeArgsStr[i];
            if (c == '<')
                depth++;
            else if (c == '>')
                depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(typeArgsStr.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        if (start < typeArgsStr.Length)
            result.Add(typeArgsStr.Substring(start).Trim());

        return result;
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

        // Check if this is a Pico.DI registration method
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (
            containingType is null
            || (!containingType.StartsWith("Pico.DI") && !containingType.Contains("SvcContainer"))
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
        sb.AppendLine("namespace Pico.DI.Gen;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Auto-generated service registrations with AOT-compatible factory methods."
        );
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GeneratedServiceRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all scanned services with pre-compiled factory methods.");
        sb.AppendLine(
            "    /// This method is auto-generated by scanning Register* method calls in the codebase."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static global::Pico.DI.Abs.ISvcContainer ConfigureGeneratedServices(this global::Pico.DI.Abs.ISvcContainer container)"
        );
        sb.AppendLine("    {");

        foreach (var reg in registrations)
        {
            var lifetimeEnum = $"global::Pico.DI.Abs.SvcLifetime.{reg.Lifetime}";

            if (reg.ConstructorParameters.IsEmpty)
            {
                // Simple case: parameterless constructor
                sb.AppendLine($"        container.Register(new global::Pico.DI.Abs.SvcDescriptor(");
                sb.AppendLine($"            typeof({reg.ServiceTypeFullName}),");
                sb.AppendLine($"            static _ => new {reg.ImplementationTypeFullName}(),");
                sb.AppendLine($"            {lifetimeEnum}));");
            }
            else
            {
                // Constructor with dependencies
                sb.AppendLine($"        container.Register(new global::Pico.DI.Abs.SvcDescriptor(");
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
                sb.AppendLine($"            {lifetimeEnum}));");
            }

            sb.AppendLine();
        }

        sb.AppendLine("        return container;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
