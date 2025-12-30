namespace Pico.DI.Gen;

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
    int TypeParameterCount,
    ImmutableArray<string> TypeParameterNames, // e.g., ["T", "TKey"]
    ImmutableArray<OpenGenericConstructorParameter> ConstructorParameters // Constructor parameters of the open generic type
);

/// <summary>
/// Represents a constructor parameter in an open generic type.
/// The TypeFullName may contain type parameters (e.g., "T" or "ILogger&lt;T&gt;").
/// </summary>
internal record OpenGenericConstructorParameter(
    string TypeFullName,
    string TypeName,
    string ParameterName,
    bool IsTypeParameter, // Whether it is a type parameter itself (e.g., T)
    bool ContainsTypeParameter // Whether it contains a type parameter (e.g., ILogger<T>)
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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all invocations that look like Register* method calls (regular registrations)
        var registerInvocations = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsRegisterMethodInvocation(node),
                transform: static (ctx, _) => GetInvocationInfo(ctx)
            )
            .Where(static x => x is not null);

        // Find all open generic registrations (from both regular Register* and legacy RegisterOpenGeneric* methods)
        var openGenericRegistrations = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsOpenGenericRegisterInvocation(node),
                transform: static (ctx, _) => GetOpenGenericInvocationInfo(ctx)
            )
            .Where(static x => x is not null);

        // Find all GetService<T> calls to detect closed generic usages
        var closedGenericUsages = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsGetServiceInvocation(node),
                transform: static (ctx, _) => GetClosedGenericUsageInfo(ctx)
            )
            .Where(static x => x is not null);

        // Combine all sources with compilation
        var compilationAndInvocations = context
            .CompilationProvider
            .Combine(registerInvocations.Collect())
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
    /// Check if this is an open generic registration invocation.
    /// Detects Register* methods with typeof(T&lt;&gt;) arguments.
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

        // Check for Register* methods with typeof() arguments that might be open generics
        if (methodName is not null && RegisterMethodNames.Any(m => methodName.StartsWith(m)))
        {
            // Look for typeof(T<>) patterns in arguments
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    // Check if it looks like an open generic (has <> in the text)
                    var typeText = typeOfExpr.Type.ToString();
                    if (typeText.Contains("<>") || typeText.Contains("<,"))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Fast syntax-only check to filter potential Register* method calls.
    /// Excludes open generic registrations which are handled separately.
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

        // Check if it's a regular Register* method
        if (methodName is null || !RegisterMethodNames.Any(m => methodName.StartsWith(m)))
            return false;

        // Exclude invocations with typeof(T<>) which are open generic registrations
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeText = typeOfExpr.Type.ToString();
                if (typeText.Contains("<>") || typeText.Contains("<,"))
                    return false;
            }
        }

        return true;
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
        // C# 14 extension types may have containing types like "__extension__ISvcContainer" or similar
        var containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        var containingNs = methodSymbol.ContainingNamespace?.ToDisplayString() ?? "";

        var isPicoDiMethod =
            containingType.StartsWith("Pico.DI")
            || containingType.Contains("SvcContainer")
            || containingType.Contains("ISvcContainer")
            || containingNs.StartsWith("Pico.DI");

        if (!isPicoDiMethod)
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
    /// Extract open generic registration info from Register* methods with open generic type arguments.
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
        // C# 14 extension types may have containing types like "__extension__ISvcContainer" or similar
        var containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        var containingNs = methodSymbol.ContainingNamespace?.ToDisplayString() ?? "";

        var isPicoDiMethod =
            containingType.StartsWith("Pico.DI")
            || containingType.Contains("SvcContainer")
            || containingType.Contains("ISvcContainer")
            || containingNs.StartsWith("Pico.DI");

        if (!isPicoDiMethod)
            return null;

        var methodName = methodSymbol.Name;

        // Check if it's a Register* method with open generic arguments
        if (RegisterMethodNames.Contains(methodName))
        {
            // Verify it has open generic type arguments
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
                    if (typeSymbol is INamedTypeSymbol { IsUnboundGenericType: true })
                        return new OpenGenericInvocationInfo(invocation, semanticModel);
                }
            }
        }

        return null;
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

        // Process closed generic usages (from GetService<T> calls)
        var closedUsages = closedGenericUsages
            .Where(x => x is not null)
            .Select(x => AnalyzeClosedGenericUsage(x!.ClosedGenericType))
            .OfType<ClosedGenericUsage>()
            .Distinct()
            .ToList();

        // Also detect closed generic usages referenced in constructor parameters of registered services
        var ctorClosedUsages = registrations
            .SelectMany(r => r.ConstructorParameters)
            .Where(p => p.TypeFullName.Contains("<")) // simple heuristic for generics
            .Select(p =>
            {
                var full = p.TypeFullName;
                var angleIdx = full.IndexOf('<');
                if (angleIdx < 0)
                    return null;

                var baseName = full.Substring(0, angleIdx);
                var typeArgsStr = full.Substring(angleIdx + 1, full.Length - angleIdx - 2);
                var argList = ParseTypeArguments(typeArgsStr).ToImmutableArray();

                // Build open generic form matching AnalyzeOpenGenericInvocation output, e.g., global::Ns.ILog<> or global::Ns.IGeneric<,>
                var openGenericArityPlaceholder =
                    argList.Length > 0 ? new string(',', argList.Length - 1) : string.Empty;
                var openFullName = $"{baseName}<{openGenericArityPlaceholder}>";

                // Exclude System types
                if (baseName.StartsWith("global::System"))
                    return null;

                return new ClosedGenericUsage(full, openFullName, argList);
            })
            .Where(x => x is not null)
            .Cast<ClosedGenericUsage>()
            .Distinct()
            .ToList();

        // Merge closed usages from GetService<T> and from constructor parameters
        foreach (var cu in ctorClosedUsages)
        {
            if (!closedUsages.Contains(cu))
                closedUsages.Add(cu);
        }

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

        // Compile-time circular dependency detection
        var circularDependencies = DetectCircularDependencies(allRegistrations);
        foreach (var cycle in circularDependencies)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(CircularDependencyDiagnostic, Location.None, cycle)
            );
        }

        var source = GenerateSource(allRegistrations);
        context.AddSource(
            "PicoIoC.ServiceRegistrations.g.cs",
            SourceText.From(source, Encoding.UTF8)
        );
    }

    private static readonly DiagnosticDescriptor CircularDependencyDiagnostic =
        new(
            "PICO010",
            "Circular dependency detected",
            "Circular dependency detected at compile-time: {0}",
            "Pico.DI",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A circular dependency chain was detected which will cause a runtime exception. Fix the dependency cycle."
        );

    /// <summary>
    /// Detects circular dependencies at compile-time by analyzing the dependency graph.
    /// </summary>
    private static List<string> DetectCircularDependencies(List<ServiceRegistration> registrations)
    {
        var cycles = new List<string>();

        // Build a dependency graph: ServiceType -> List of dependency types
        var dependencyGraph = new Dictionary<string, HashSet<string>>();
        var serviceTypes = new HashSet<string>();

        foreach (var reg in registrations)
        {
            serviceTypes.Add(reg.ServiceTypeFullName);
            if (!dependencyGraph.ContainsKey(reg.ServiceTypeFullName))
            {
                dependencyGraph[reg.ServiceTypeFullName] =  [];
            }

            foreach (var param in reg.ConstructorParameters)
            {
                dependencyGraph[reg.ServiceTypeFullName].Add(param.TypeFullName);
            }
        }

        // DFS to detect cycles
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var serviceType in serviceTypes)
        {
            if (DetectCycleDfs(serviceType, dependencyGraph, visited, recursionStack, path, cycles))
            {
                // Found a cycle, already added to cycles list
            }
        }

        return cycles;
    }

    private static bool DetectCycleDfs(
        string current,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<string> cycles
    )
    {
        if (recursionStack.Contains(current))
        {
            // Found a cycle - extract the cycle path
            var cycleStart = path.IndexOf(current);
            if (cycleStart >= 0)
            {
                var cyclePath = path.Skip(cycleStart).Append(current).ToList();
                var cycleStr = string.Join(" -> ", cyclePath.Select(GetSimpleName));
                if (!cycles.Contains(cycleStr))
                {
                    cycles.Add(cycleStr);
                }
            }
            return true;
        }

        if (visited.Contains(current))
            return false;

        visited.Add(current);
        recursionStack.Add(current);
        path.Add(current);

        if (graph.TryGetValue(current, out var dependencies))
        {
            foreach (var dep in dependencies)
            {
                DetectCycleDfs(dep, graph, visited, recursionStack, path, cycles);
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(current);
        return false;
    }

    /// <summary>
    /// Analyze an open generic registration call from Register* methods with open generic arguments.
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

        // Accept Register* methods
        if (methodName is null || !RegisterMethodNames.Contains(methodName))
            return null;

        // Parse arguments: typically (typeof(IRepository<>), typeof(Repository<>), SvcLifetime.X)
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
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
        if (methodName!.Contains("Transient"))
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

        // Get the original definition of the open generic implementation type to extract constructor parameters
        var openImplType = namedImplType.OriginalDefinition;
        var typeParamNames = openImplType.TypeParameters.Select(tp => tp.Name).ToImmutableArray();

        // Extract constructor parameters, marking which ones involve type parameters
        var ctorParams = GetOpenGenericConstructorParameters(openImplType, typeParamNames);

        return new OpenGenericRegistration(
            namedServiceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            namedImplType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            lifetime,
            namedServiceType.TypeParameters.Length,
            typeParamNames,
            ctorParams
        );
    }

    /// <summary>
    /// Gets the constructor parameters of an open generic type, marking type parameter usage.
    /// </summary>
    private static ImmutableArray<OpenGenericConstructorParameter> GetOpenGenericConstructorParameters(
        INamedTypeSymbol openGenericType,
        ImmutableArray<string> typeParameterNames
    )
    {
        var constructors = openGenericType
            .Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();

        var constructor = constructors.FirstOrDefault();
        if (constructor is null)
            return ImmutableArray<OpenGenericConstructorParameter>.Empty;

        return
        [
            .. constructor.Parameters.Select(p =>
            {
                var typeFullName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var isTypeParam = p.Type is ITypeParameterSymbol;
                var containsTypeParam = ContainsTypeParameter(p.Type, typeParameterNames);

                return new OpenGenericConstructorParameter(
                    typeFullName,
                    p.Type.Name,
                    p.Name,
                    isTypeParam,
                    containsTypeParam
                );
            })
        ];
    }

    /// <summary>
    /// Checks whether a type contains any of the specified type parameters.
    /// </summary>
    private static bool ContainsTypeParameter(
        ITypeSymbol type,
        ImmutableArray<string> typeParameterNames
    )
    {
        if (type is ITypeParameterSymbol tp)
            return typeParameterNames.Contains(tp.Name);

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            return namedType.TypeArguments.Any(ta => ContainsTypeParameter(ta, typeParameterNames));

        return false;
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
            .TypeArguments
            .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
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
            var openGeneric = openGenerics.FirstOrDefault(
                og => og.OpenServiceTypeFullName == usage.OpenServiceTypeFullName
            );

            if (openGeneric is null)
                continue;

            // Build closed implementation type by applying type arguments
            var closedImplTypeFullName = BuildClosedGenericTypeName(
                openGeneric.OpenImplementationTypeFullName,
                usage.TypeArgumentsFullNames
            );

            // Build type parameter mapping: T -> User, TKey -> int, etc.
            var typeParamMap = new Dictionary<string, string>();
            for (
                var i = 0;
                i < openGeneric.TypeParameterNames.Length
                    && i < usage.TypeArgumentsFullNames.Length;
                i++
            )
            {
                typeParamMap[openGeneric.TypeParameterNames[i]] = usage.TypeArgumentsFullNames[i];
            }

            // Generate closed type constructor parameters based on open generic constructor parameters
            var constructorParams = openGeneric
                .ConstructorParameters
                .Select(
                    p =>
                        new ConstructorParameter(
                            SubstituteTypeParameters(p.TypeFullName, typeParamMap),
                            p.TypeName,
                            p.ParameterName
                        )
                )
                .ToImmutableArray();

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
    /// Substitutes type parameters in the type full name with actual types.
    /// Example: "T" -> "global::MyApp.User"
    ///          "global::Microsoft.Extensions.Logging.ILogger&lt;T&gt;" -> "global::Microsoft.Extensions.Logging.ILogger&lt;global::MyApp.User&gt;"
    /// </summary>
    private static string SubstituteTypeParameters(
        string typeFullName,
        Dictionary<string, string> typeParamMap
    )
    {
        var result = typeFullName;

        foreach (var kvp in typeParamMap)
        {
            var paramName = kvp.Key;
            var actualType = kvp.Value;

            // Replace standalone type parameter (e.g., "T" as the complete type)
            if (result == paramName)
            {
                result = actualType;
                continue;
            }

            // Replace type parameter within generic arguments (e.g., T in "ILogger<T>")
            // Handle formats: <T> or <T, U> or <SomeType, T>
            result = SubstituteTypeParameterInGeneric(result, paramName, actualType);
        }

        return result;
    }

    /// <summary>
    /// Replaces type parameters within a generic type name.
    /// </summary>
    private static string SubstituteTypeParameterInGeneric(
        string typeFullName,
        string paramName,
        string actualType
    )
    {
        // Simple case: type parameter appears as generic argument
        // Match: <T> or <T, or , T> or , T,
        var patterns = new[]
        {
            ($"<{paramName}>", $"<{actualType}>"),
            ($"<{paramName},", $"<{actualType},"),
            ($", {paramName}>", $", {actualType}>"),
            ($", {paramName},", $", {actualType},")
        };

        var result = typeFullName;
        foreach (var (pattern, replacement) in patterns)
        {
            result = result.Replace(pattern, replacement);
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

        // Verify this is a Pico.DI registration method
        // C# 14 extension types may have containing types like "__extension__ISvcContainer" or similar
        var containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        var containingNs = methodSymbol.ContainingNamespace?.ToDisplayString() ?? "";

        var isPicoDiMethod =
            containingType.StartsWith("Pico.DI")
            || containingType.Contains("SvcContainer")
            || containingType.Contains("ISvcContainer")
            || containingNs.StartsWith("Pico.DI");

        if (!isPicoDiMethod)
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
            .Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
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
        sb.AppendLine("/// Optimized with inlined resolution chains for Transient services.");
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

        // Build lookup for registrations by service type for inlining
        var registrationLookup = registrations
            .GroupBy(r => r.ServiceTypeFullName)
            .ToDictionary(g => g.Key, g => g.Last()); // Use last registration (override pattern)

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
                // Constructor with dependencies - use inlined resolution for Transient deps
                sb.AppendLine($"        container.Register(new global::Pico.DI.Abs.SvcDescriptor(");
                sb.AppendLine($"            typeof({reg.ServiceTypeFullName}),");

                // Generate inlined factory
                var factoryCode = GenerateInlinedFactory(
                    reg,
                    registrationLookup,
                    new HashSet<string>(),
                    0
                );
                sb.AppendLine($"            static scope => {factoryCode},");
                sb.AppendLine($"            {lifetimeEnum}));");
            }

            sb.AppendLine();
        }

        // Generate IEnumerable<T> registrations for AOT compatibility
        sb.AppendLine("        // IEnumerable<T> registrations for AOT compatibility");
        var groupedByServiceType = registrations
            .GroupBy(r => r.ServiceTypeFullName)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var group in groupedByServiceType)
        {
            var serviceTypeFullName = group.Key;
            var regs = group.Value;

            sb.AppendLine($"        container.Register(new global::Pico.DI.Abs.SvcDescriptor(");
            sb.AppendLine(
                $"            typeof(global::System.Collections.Generic.IEnumerable<{serviceTypeFullName}>),"
            );
            sb.AppendLine($"            static scope => new {serviceTypeFullName}[]");
            sb.AppendLine($"            {{");

            for (var i = 0; i < regs.Count; i++)
            {
                var reg = regs[i];
                var comma = i < regs.Count - 1 ? "," : "";

                if (reg.ConstructorParameters.IsEmpty)
                {
                    sb.AppendLine($"                new {reg.ImplementationTypeFullName}(){comma}");
                }
                else
                {
                    var factoryCode = GenerateInlinedFactory(
                        reg,
                        registrationLookup,
                        new HashSet<string>(),
                        4
                    );
                    sb.AppendLine($"                {factoryCode}{comma}");
                }
            }

            sb.AppendLine($"            }},");
            sb.AppendLine($"            global::Pico.DI.Abs.SvcLifetime.Transient));");
            sb.AppendLine();
        }

        sb.AppendLine(
            "        // If the concrete container supports Build(), freeze registrations for optimal lookup."
        );
        sb.AppendLine(
            "        if (container is global::Pico.DI.SvcContainer svcContainer) svcContainer.Build();"
        );
        sb.AppendLine("        return container;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates an inlined factory expression for the given registration.
    /// For Transient dependencies, recursively inlines the construction.
    /// For Singleton/Scoped dependencies, calls scope.GetService().
    /// </summary>
    private static string GenerateInlinedFactory(
        ServiceRegistration reg,
        Dictionary<string, ServiceRegistration> registrationLookup,
        HashSet<string> visitedTypes,
        int indentLevel
    )
    {
        var indent = new string(' ', 16 + indentLevel * 4);

        if (reg.ConstructorParameters.IsEmpty)
        {
            return $"new {reg.ImplementationTypeFullName}()";
        }

        var paramExpressions = new List<string>();

        foreach (var param in reg.ConstructorParameters)
        {
            var paramExpr = GenerateParameterExpression(
                param.TypeFullName,
                registrationLookup,
                visitedTypes,
                indentLevel + 1
            );
            paramExpressions.Add(paramExpr);
        }

        if (paramExpressions.Count == 1)
        {
            return $"new {reg.ImplementationTypeFullName}({paramExpressions[0]})";
        }

        // Multi-line format for multiple parameters
        var sb = new StringBuilder();
        sb.Append($"new {reg.ImplementationTypeFullName}(");

        for (var i = 0; i < paramExpressions.Count; i++)
        {
            var comma = i < paramExpressions.Count - 1 ? "," : "";
            if (i == 0)
            {
                sb.Append(paramExpressions[i] + comma);
            }
            else
            {
                sb.Append(" " + paramExpressions[i] + comma);
            }
        }
        sb.Append(")");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the expression for a constructor parameter.
    /// Inlines Transient dependencies, uses GetService for Singleton/Scoped.
    /// </summary>
    private static string GenerateParameterExpression(
        string paramTypeFullName,
        Dictionary<string, ServiceRegistration> registrationLookup,
        HashSet<string> visitedTypes,
        int indentLevel
    )
    {
        // Check if we have a registration for this type
        if (registrationLookup.TryGetValue(paramTypeFullName, out var depReg))
        {
            // Only inline Transient dependencies to avoid breaking singleton/scoped semantics
            if (depReg.Lifetime == "Transient")
            {
                // Check for circular dependency
                if (visitedTypes.Contains(paramTypeFullName))
                {
                    // Fall back to GetService for circular references
                    return $"({paramTypeFullName})scope.GetService(typeof({paramTypeFullName}))";
                }

                // Mark as visited and recursively inline
                var newVisited = new HashSet<string>(visitedTypes) { paramTypeFullName };
                return GenerateInlinedFactory(depReg, registrationLookup, newVisited, indentLevel);
            }
        }

        // For Singleton, Scoped, or unknown dependencies, use GetService
        return $"({paramTypeFullName})scope.GetService(typeof({paramTypeFullName}))";
    }
}
