using Pico.DI.Gen.Constants;

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
    ImmutableArray<string> ConstructorParameters // TypeFullNames of constructor parameters
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
    ImmutableArray<string> ConstructorParameters // TypeFullNames of constructor parameters (may contain type parameters)
);

/// <summary>
/// Represents a closed generic type usage that needs to be pre-generated for AOT.
/// </summary>
internal record ClosedGenericUsage(
    string ClosedServiceTypeFullName, // e.g., "IRepository<User>"
    string OpenServiceTypeFullName, // e.g., "IRepository<>"
    ImmutableArray<string> TypeArgumentsFullNames // e.g., ["User"]
);

/// <summary>
/// Source Generator that scans all ISvcContainer.Register* method calls
/// and generates AOT-compatible factory methods at compile time.
/// Also handles open generic registrations by detecting closed generic usages.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
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

        // Find closed generic types used in declarations (variables, fields, properties, parameters)
        // This helps detect entity-associated generics like IRepository<User>
        var closedGenericDeclarations = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsClosedGenericTypeDeclaration(node),
                transform: static (ctx, _) => GetClosedGenericFromDeclaration(ctx)
            )
            .Where(static x => x is not null);

        // Find closed generic types in constructor parameters (regular and primary constructors)
        // This detects ILogger<Service>, IRepository<Entity> etc. in constructor injection
        var closedGenericCtorParams = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsConstructorWithGenericParameter(node),
                transform: static (ctx, _) => GetClosedGenericsFromConstructor(ctx)
            )
            .Where(static x => x is not null)
            .SelectMany(static (x, _) => x);

        // Combine all sources with compilation for referenced assembly scanning
        var combinedSources = registerInvocations
            .Collect()
            .Combine(openGenericRegistrations.Collect())
            .Combine(closedGenericUsages.Collect())
            .Combine(closedGenericDeclarations.Collect())
            .Combine(closedGenericCtorParams.Collect())
            .Combine(context.CompilationProvider);

        // Generate source
        context.RegisterSourceOutput(
            combinedSources,
            static (spc, source) =>
            {
                var (
                    ((((invocations, openGenerics), closedUsages), closedDeclarations),
                    ctorClosedGenerics),
                    compilation
                ) = source;
                Execute(
                    invocations,
                    openGenerics,
                    closedUsages,
                    closedDeclarations,
                    ctorClosedGenerics,
                    compilation,
                    spc
                );
            }
        );
    }

    /// <summary>
    /// Check if this is a closed generic type used in a declaration (variable, field, property, parameter).
    /// This helps detect entity-associated generics like IRepository&lt;User&gt;.
    /// Also detects closed generic types in constructor parameters (both regular and primary constructors).
    /// </summary>
    private static bool IsClosedGenericTypeDeclaration(SyntaxNode node)
    {
        // Check for generic name syntax in type declarations
        if (node is not GenericNameSyntax genericName)
            return false;

        // Must have type arguments (not open generic)
        if (genericName.TypeArgumentList.Arguments.Count == 0)
            return false;

        // Check if it's part of a type declaration context
        var parent = genericName.Parent;
        while (parent is QualifiedNameSyntax or AliasQualifiedNameSyntax or NullableTypeSyntax)
            parent = parent.Parent;

        // Check standard declaration contexts
        if (
            parent
            is VariableDeclarationSyntax
                or // var x = ...; or Type x = ...;
                PropertyDeclarationSyntax
                or // public Type Property { get; }
                FieldDeclarationSyntax
                or // private Type _field;
                ParameterSyntax
                or // void Method(Type param) or constructor parameters
                TypeArgumentListSyntax
                or // Generic<Type>
                BaseTypeSyntax
                or // class Foo : IBase<Type>
                ObjectCreationExpressionSyntax
        ) // new Type()
            return true;

        // Additionally check if this is a constructor parameter by traversing up to find ConstructorDeclarationSyntax
        // or primary constructor in class/record/struct declarations
        if (parent is ParameterSyntax paramSyntax)
        {
            var paramListParent = paramSyntax.Parent?.Parent;
            return paramListParent
                is ConstructorDeclarationSyntax
                    or ClassDeclarationSyntax // Primary constructor (C# 12+)
                    or RecordDeclarationSyntax // Record primary constructor
                    or StructDeclarationSyntax; // Struct primary constructor
        }

        return false;
    }

    /// <summary>
    /// Check if this is a constructor declaration (including primary constructors).
    /// Used to detect closed generic types in constructor parameters.
    /// </summary>
    private static bool IsConstructorWithGenericParameter(SyntaxNode node)
    {
        // Check for regular constructor declarations
        if (node is ConstructorDeclarationSyntax ctorDecl)
        {
            return ctorDecl
                .ParameterList
                .Parameters
                .Any(
                    p =>
                        p.Type is GenericNameSyntax
                        || (p.Type is QualifiedNameSyntax qns && qns.Right is GenericNameSyntax)
                        || (
                            p.Type is NullableTypeSyntax nts && nts.ElementType is GenericNameSyntax
                        )
                );
        }

        // Check for primary constructor in class/record/struct (C# 12+)
        if (node is TypeDeclarationSyntax typeDecl && typeDecl.ParameterList is not null)
        {
            return typeDecl
                .ParameterList
                .Parameters
                .Any(
                    p =>
                        p.Type is GenericNameSyntax
                        || (p.Type is QualifiedNameSyntax qns && qns.Right is GenericNameSyntax)
                        || (
                            p.Type is NullableTypeSyntax nts && nts.ElementType is GenericNameSyntax
                        )
                );
        }

        return false;
    }

    /// <summary>
    /// Extract closed generic types from constructor parameters.
    /// </summary>
    private static IEnumerable<ITypeSymbol> GetClosedGenericsFromConstructor(
        GeneratorSyntaxContext context
    )
    {
        var semanticModel = context.SemanticModel;
        IEnumerable<ParameterSyntax> parameters;

        if (context.Node is ConstructorDeclarationSyntax ctorDecl)
        {
            parameters = ctorDecl.ParameterList.Parameters;
        }
        else if (
            context.Node is TypeDeclarationSyntax typeDecl
            && typeDecl.ParameterList is not null
        )
        {
            parameters = typeDecl.ParameterList.Parameters;
        }
        else
        {
            yield break;
        }

        foreach (var param in parameters)
        {
            if (param.Type is null)
                continue;

            var typeInfo = semanticModel.GetTypeInfo(param.Type);
            var typeSymbol = typeInfo.Type;

            // Check if it's a closed generic type
            if (
                typeSymbol
                is not INamedTypeSymbol
                {
                    IsGenericType: true,
                    IsUnboundGenericType: false
                } namedType
            )
                continue;

            // Skip if any type argument is a type parameter (e.g., T in ILog<T> inside a generic class)
            if (namedType.TypeArguments.Any(ta => ta is ITypeParameterSymbol))
                continue;

            // Skip System types
            var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";
            if (ns.StartsWith(PicoDiNames.SystemNamespace))
                continue;

            yield return namedType;
        }
    }

    /// <summary>
    /// Extract closed generic type info from declaration syntax.
    /// </summary>
    private static ITypeSymbol? GetClosedGenericFromDeclaration(GeneratorSyntaxContext context)
    {
        if (context.Node is not GenericNameSyntax genericName)
            return null;

        var semanticModel = context.SemanticModel;
        var typeInfo = semanticModel.GetTypeInfo(genericName);
        var typeSymbol = typeInfo.Type;

        // Must be a closed generic type
        if (
            typeSymbol
            is not INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } namedType
        )
            return null;

        // Skip if any type argument is a type parameter (e.g., T in ILog<T> inside a generic class)
        // We only want fully closed generics like IRepository<User>
        if (namedType.TypeArguments.Any(ta => ta is ITypeParameterSymbol))
            return null;

        // Skip System types
        var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";
        return ns.StartsWith(PicoDiNames.SystemNamespace) ? null : namedType;
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

        return methodName is PicoDiNames.GetService or PicoDiNames.GetServices;
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
        if (
            methodName is null
            || !PicoDiNames.RegisterMethodNames.Any(m => methodName.StartsWith(m))
        )
            return false;
        // Look for typeof(T<>) patterns in arguments
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                continue;
            // Check if it looks like an open generic (has <> in the text)
            var typeText = typeOfExpr.Type.ToString();
            if (typeText.Contains("<>") || typeText.Contains("<,"))
                return true;
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
        if (
            methodName is null
            || !PicoDiNames.RegisterMethodNames.Any(m => methodName.StartsWith(m))
        )
            return false;

        // Exclude invocations with typeof(T<>) which are open generic registrations
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                continue;
            var typeText = typeOfExpr.Type.ToString();
            if (typeText.Contains("<>") || typeText.Contains("<,"))
                return false;
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

        // Also check receiver type for extension methods
        var receiverType = methodSymbol.ReceiverType?.ToDisplayString() ?? "";
        var reducedFrom = methodSymbol.ReducedFrom?.ContainingNamespace?.ToDisplayString() ?? "";

        var isPicoDiMethod =
            containingType.StartsWith(PicoDiNames.RootNamespace)
            || containingType.Contains(PicoDiNames.SvcContainer)
            || containingType.Contains(PicoDiNames.ISvcContainer)
            || containingNs.StartsWith(PicoDiNames.RootNamespace)
            || receiverType.Contains(PicoDiNames.ISvcContainer)
            || receiverType.Contains(PicoDiNames.SvcContainer)
            || reducedFrom.StartsWith(PicoDiNames.RootNamespace);

        if (!isPicoDiMethod)
            return null;

        var methodName = methodSymbol.Name;
        return !PicoDiNames.RegisterMethodNames.Contains(methodName)
            ? null
            : new InvocationInfo(invocation, semanticModel);
    }

    private record InvocationInfo(
        InvocationExpressionSyntax Invocation,
        SemanticModel SemanticModel
    );

    private record OpenGenericInvocationInfo(
        InvocationExpressionSyntax Invocation,
        SemanticModel SemanticModel
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

        // Also check receiver type for extension methods
        var receiverType = methodSymbol.ReceiverType?.ToDisplayString() ?? "";
        var reducedFrom = methodSymbol.ReducedFrom?.ContainingNamespace?.ToDisplayString() ?? "";

        var isPicoDiMethod =
            containingType.StartsWith(PicoDiNames.RootNamespace)
            || containingType.Contains(PicoDiNames.SvcContainer)
            || containingType.Contains(PicoDiNames.ISvcContainer)
            || containingNs.StartsWith(PicoDiNames.RootNamespace)
            || receiverType.Contains(PicoDiNames.ISvcContainer)
            || receiverType.Contains(PicoDiNames.SvcContainer)
            || reducedFrom.StartsWith(PicoDiNames.RootNamespace);

        if (!isPicoDiMethod)
            return null;

        var methodName = methodSymbol.Name;

        // Check if it's a Register* method with open generic arguments
        if (!PicoDiNames.RegisterMethodNames.Contains(methodName))
            return null;
        // Verify it has open generic type arguments
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                continue;
            var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
            if (typeSymbol is INamedTypeSymbol { IsUnboundGenericType: true })
                return new OpenGenericInvocationInfo(invocation, semanticModel);
        }

        return null;
    }

    /// <summary>
    /// Extract closed generic usage from GetService&lt;T&gt; calls.
    /// </summary>
    private static ITypeSymbol? GetClosedGenericUsageInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Check if it's a GetService<T> or GetServices<T> method
        if (methodSymbol.Name is not (PicoDiNames.GetService or PicoDiNames.GetServices))
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
        return ns.StartsWith(PicoDiNames.SystemNamespace) ? null : namedType;
    }

    private static void Execute(
        ImmutableArray<InvocationInfo?> invocations,
        ImmutableArray<OpenGenericInvocationInfo?> openGenericInvocations,
        ImmutableArray<ITypeSymbol?> closedGenericUsages,
        ImmutableArray<ITypeSymbol?> closedGenericDeclarations,
        ImmutableArray<ITypeSymbol> ctorClosedGenerics,
        Compilation compilation,
        SourceProductionContext context
    )
    {
        // Process regular registrations
        var registrations = invocations
            .Where(x => x is not null)
            .Select(x => AnalyzeInvocation(x!.Invocation, x.SemanticModel))
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
            .Select(x => AnalyzeClosedGenericUsage(x!))
            .OfType<ClosedGenericUsage>()
            .Distinct()
            .ToList();

        // Process closed generic usages from type declarations (variables, fields, properties, parameters)
        // This detects entity-associated generics like IRepository<User>
        var declarationClosedUsages = closedGenericDeclarations
            .Where(x => x is not null)
            .Select(x => AnalyzeClosedGenericUsage(x!))
            .OfType<ClosedGenericUsage>()
            .Distinct()
            .ToList();

        // Merge declaration usages
        foreach (var du in declarationClosedUsages.Where(du => !closedUsages.Contains(du)))
        {
            closedUsages.Add(du);
        }

        // Process closed generic usages from constructor parameters (regular and primary constructors)
        // This detects ILogger<Service>, IRepository<Entity> etc. in constructor injection
        var ctorParamClosedUsages = ctorClosedGenerics
            .Select(x => AnalyzeClosedGenericUsage(x))
            .OfType<ClosedGenericUsage>()
            .Distinct()
            .ToList();

        // Merge constructor parameter usages
        foreach (var cu in ctorParamClosedUsages.Where(cu => !closedUsages.Contains(cu)))
        {
            closedUsages.Add(cu);
        }

        // Also detect closed generic usages referenced in constructor parameters of registered services
        var ctorClosedUsages = registrations
            .SelectMany(r => r.ConstructorParameters)
            .Where(typeFullName => typeFullName.Contains("<")) // simple heuristic for generics
            .Select(typeFullName =>
            {
                var angleIdx = typeFullName.IndexOf('<');
                if (angleIdx < 0)
                    return null;

                var baseName = typeFullName.Substring(0, angleIdx);
                var typeArgsStr = typeFullName.Substring(
                    angleIdx + 1,
                    typeFullName.Length - angleIdx - 2
                );
                var argList = ParseTypeArguments(typeArgsStr).ToImmutableArray();

                // Build open generic form matching AnalyzeOpenGenericInvocation output, e.g., global::Ns.ILog<> or global::Ns.IGeneric<,>
                var openGenericArityPlaceholder =
                    argList.Length > 0 ? new string(',', argList.Length - 1) : string.Empty;
                var openFullName = $"{baseName}<{openGenericArityPlaceholder}>";

                // Exclude System types
                return baseName.StartsWith(PicoDiNames.GlobalSystemPrefix)
                    ? null
                    : new ClosedGenericUsage(typeFullName, openFullName, argList);
            })
            .Where(x => x is not null)
            .Cast<ClosedGenericUsage>()
            .Distinct()
            .ToList();

        // Merge closed usages from GetService<T> and from constructor parameters
        foreach (var cu in ctorClosedUsages.Where(cu => !closedUsages.Contains(cu)))
        {
            closedUsages.Add(cu);
        }

        // Discover open generic registrations from referenced assemblies' metadata classes
        var discoveredOpenGenerics = DiscoverOpenGenericsFromReferencedAssemblies(compilation, openGenerics);
        foreach (var og in discoveredOpenGenerics.Where(og => !openGenerics.Contains(og)))
        {
            openGenerics.Add(og);
        }

        // Generate closed generic registrations from open generic + usages
        var generatedClosedGenerics = GenerateClosedGenericRegistrations(
            openGenerics,
            closedUsages
        );

        // Combine all registrations
        var allRegistrations = registrations.Concat(generatedClosedGenerics).Distinct().ToList();

        if (allRegistrations.Count == 0 && openGenerics.Count == 0)
            return;

        // Compile-time circular dependency detection
        var circularDependencies = DetectCircularDependencies(allRegistrations);
        foreach (var cycle in circularDependencies)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(CircularDependencyDiagnostic, Location.None, cycle)
            );
        }

        var source = GenerateSource(allRegistrations, compilation);
        context.AddSource(
            "PicoIoC.ServiceRegistrations.g.cs",
            SourceText.From(source, Encoding.UTF8)
        );

        // Generate metadata class for this project's open generic registrations
        // so other projects can discover them
        if (openGenerics.Count > 0)
        {
            var metadataSource = GenerateOpenGenericMetadata(openGenerics, compilation);
            context.AddSource(
                "PicoIoC.OpenGenericMetadata.g.cs",
                SourceText.From(metadataSource, Encoding.UTF8)
            );
        }
    }

    /// <summary>
    /// Discovers open generic registrations from referenced assemblies by scanning for
    /// PicoDiOpenGenericMetadata classes generated by the source generator.
    /// </summary>
    private static List<OpenGenericRegistration> DiscoverOpenGenericsFromReferencedAssemblies(
        Compilation compilation,
        List<OpenGenericRegistration> existingOpenGenerics
    )
    {
        var discovered = new List<OpenGenericRegistration>();
        var existingOpenNames = new HashSet<string>(existingOpenGenerics.Select(og => og.OpenServiceTypeFullName));

        // Scan referenced assemblies for metadata classes
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                continue;

            // Look for PicoDiOpenGenericMetadata class in any namespace
            var metadataTypes = FindMetadataTypes(assembly.GlobalNamespace);

            foreach (var metadataType in metadataTypes)
            {
                // Read the static fields/properties that contain registration info
                var registrations = ExtractOpenGenericRegistrationsFromMetadata(metadataType);
                foreach (var reg in registrations)
                {
                    if (!existingOpenNames.Contains(reg.OpenServiceTypeFullName))
                    {
                        discovered.Add(reg);
                        existingOpenNames.Add(reg.OpenServiceTypeFullName);
                    }
                }
            }
        }

        return discovered;
    }

    /// <summary>
    /// Recursively finds all PicoDiOpenGenericMetadata types in a namespace.
    /// </summary>
    private static List<INamedTypeSymbol> FindMetadataTypes(INamespaceSymbol ns)
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var type in ns.GetTypeMembers())
        {
            if (type.Name == "PicoDiOpenGenericMetadata" &&
                type.DeclaredAccessibility == Accessibility.Public)
            {
                result.Add(type);
            }
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            result.AddRange(FindMetadataTypes(childNs));
        }

        return result;
    }

    /// <summary>
    /// Extracts open generic registration info from a metadata type's static members.
    /// </summary>
    private static List<OpenGenericRegistration> ExtractOpenGenericRegistrationsFromMetadata(INamedTypeSymbol metadataType)
    {
        var result = new List<OpenGenericRegistration>();

        // Look for static readonly fields with specific naming pattern
        foreach (var member in metadataType.GetMembers())
        {
            if (member is not IFieldSymbol field)
                continue;

            if (!field.IsStatic || !field.IsReadOnly)
                continue;

            // Parse field name pattern: OpenGeneric_{ServiceType}_{ImplType}_{Lifetime}
            // The actual type info is in attributes
            var attributes = field.GetAttributes();
            foreach (var attr in attributes)
            {
                if (attr.AttributeClass?.Name != "PicoDiOpenGenericAttribute")
                    continue;

                var args = attr.ConstructorArguments;
                if (args.Length < 4)
                    continue;

                var serviceType = args[0].Value as string ?? "";
                var implType = args[1].Value as string ?? "";
                var lifetime = args[2].Value as string ?? "Singleton";
                var typeParamCount = (int)(args[3].Value ?? 1);

                // Get type parameter names and constructor params from named arguments
                var typeParamNames = ImmutableArray<string>.Empty;
                var ctorParams = ImmutableArray<string>.Empty;

                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "TypeParameterNames" && namedArg.Value.Values.Length > 0)
                    {
                        typeParamNames = namedArg.Value.Values
                            .Select(v => v.Value as string ?? "T")
                            .ToImmutableArray();
                    }
                    else if (namedArg.Key == "ConstructorParameters" && namedArg.Value.Values.Length > 0)
                    {
                        ctorParams = namedArg.Value.Values
                            .Select(v => v.Value as string ?? "")
                            .ToImmutableArray();
                    }
                }

                if (typeParamNames.IsEmpty)
                {
                    typeParamNames = Enumerable.Range(0, typeParamCount)
                        .Select(i => i == 0 ? "T" : $"T{i}")
                        .ToImmutableArray();
                }

                result.Add(new OpenGenericRegistration(
                    serviceType,
                    implType,
                    lifetime,
                    typeParamCount,
                    typeParamNames,
                    ctorParams
                ));
            }
        }

        return result;
    }

    /// <summary>
    /// Generates a metadata class containing this project's open generic registrations.
    /// This class can be discovered by other projects' source generators.
    /// </summary>
    private static string GenerateOpenGenericMetadata(
        List<OpenGenericRegistration> openGenerics,
        Compilation compilation
    )
    {
        var sb = new StringBuilder();
        var assemblyName = compilation.AssemblyName ?? "Unknown";
        var safeAssemblyName = new string(assemblyName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Pico.DI.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Metadata class containing open generic registrations from this assembly.");
        sb.AppendLine("/// This is auto-generated by Pico.DI.Gen and used by other assemblies to discover");
        sb.AppendLine("/// open generic mappings at compile time.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine($"public static class PicoDiOpenGenericMetadata");
        sb.AppendLine("{");

        var index = 0;
        foreach (var og in openGenerics)
        {
            var typeParamNamesStr = string.Join("\", \"", og.TypeParameterNames);
            var ctorParamsStr = string.Join("\", \"", og.ConstructorParameters);

            sb.AppendLine($"    /// <summary>Open generic mapping: {og.OpenServiceTypeFullName} -> {og.OpenImplementationTypeFullName}</summary>");
            sb.AppendLine($"    [PicoDiOpenGeneric(\"{og.OpenServiceTypeFullName}\", \"{og.OpenImplementationTypeFullName}\", \"{og.Lifetime}\", {og.TypeParameterCount},");
            sb.AppendLine($"        TypeParameterNames = new[] {{ \"{typeParamNamesStr}\" }},");
            sb.AppendLine($"        ConstructorParameters = new[] {{ \"{ctorParamsStr}\" }})]");
            sb.AppendLine($"    public static readonly int Registration{index};");
            sb.AppendLine();
            index++;
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Generate the attribute class if it doesn't exist
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Attribute used to store open generic registration metadata.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[global::System.AttributeUsage(global::System.AttributeTargets.Field, AllowMultiple = false)]");
        sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine("internal sealed class PicoDiOpenGenericAttribute : global::System.Attribute");
        sb.AppendLine("{");
        sb.AppendLine("    public string ServiceType { get; }");
        sb.AppendLine("    public string ImplementationType { get; }");
        sb.AppendLine("    public string Lifetime { get; }");
        sb.AppendLine("    public int TypeParameterCount { get; }");
        sb.AppendLine("    public string[]? TypeParameterNames { get; set; }");
        sb.AppendLine("    public string[]? ConstructorParameters { get; set; }");
        sb.AppendLine();
        sb.AppendLine("    public PicoDiOpenGenericAttribute(string serviceType, string implementationType, string lifetime, int typeParameterCount)");
        sb.AppendLine("    {");
        sb.AppendLine("        ServiceType = serviceType;");
        sb.AppendLine("        ImplementationType = implementationType;");
        sb.AppendLine("        Lifetime = lifetime;");
        sb.AppendLine("        TypeParameterCount = typeParameterCount;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static readonly DiagnosticDescriptor CircularDependencyDiagnostic =
        new(
            "PICO010",
            "Circular dependency detected",
            "Circular dependency detected at compile-time: {0}",
            PicoDiNames.RootNamespace,
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

            foreach (var paramTypeFullName in reg.ConstructorParameters)
            {
                dependencyGraph[reg.ServiceTypeFullName].Add(paramTypeFullName);
            }
        }

        // DFS to detect cycles
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var serviceType in serviceTypes)
        {
            DetectCycleDfs(serviceType, dependencyGraph, visited, recursionStack, path, cycles);
        }

        return cycles;
    }

    private static void DetectCycleDfs(
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
            if (cycleStart < 0)
                return;
            var cyclePath = path.Skip(cycleStart).Append(current).ToList();
            var cycleStr = string.Join(" -> ", cyclePath.Select(GetSimpleName));
            if (!cycles.Contains(cycleStr))
            {
                cycles.Add(cycleStr);
            }

            return;
        }

        if (!visited.Add(current))
            return;

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
        if (methodName is null || !PicoDiNames.RegisterMethodNames.Contains(methodName))
            return null;

        // Parse arguments: typically (typeof(IRepository<>), typeof(Repository<>), SvcLifetime.X)
        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
            return null;

        ITypeSymbol? serviceType = null;
        ITypeSymbol? implementationType = null;
        var lifetime = PicoDiNames.Singleton;

        foreach (var arg in args)
        {
            if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
                if (typeSymbol is not INamedTypeSymbol { IsUnboundGenericType: true } unboundType)
                    continue;
                if (serviceType is null)
                    serviceType = unboundType;
                else
                    implementationType = unboundType;
            }
            else
            {
                var argType = semanticModel.GetTypeInfo(arg.Expression).Type;
                if (argType?.Name == PicoDiNames.SvcLifetime)
                {
                    lifetime = arg.Expression.ToString() switch
                    {
                        var s when s.Contains(PicoDiNames.Transient) => PicoDiNames.Transient,
                        var s when s.Contains(PicoDiNames.Scoped) => PicoDiNames.Scoped,
                        _ => PicoDiNames.Singleton
                    };
                }
            }
        }

        // Infer lifetime from method name if not explicit
        if (methodName.Contains(PicoDiNames.Transient))
            lifetime = PicoDiNames.Transient;
        else if (methodName.Contains(PicoDiNames.Scoped))
            lifetime = PicoDiNames.Scoped;
        else if (methodName.Contains(PicoDiNames.Singleton))
            lifetime = PicoDiNames.Singleton;

        if (
            serviceType is not INamedTypeSymbol namedServiceType
            || implementationType is not INamedTypeSymbol namedImplType
        )
            return null;

        // Get the original definition of the open generic implementation type to extract constructor parameters
        var openImplType = namedImplType.OriginalDefinition;
        var typeParamNames = openImplType.TypeParameters.Select(tp => tp.Name).ToImmutableArray();

        // Extract constructor parameters
        var ctorParams = GetOpenGenericConstructorParameters(openImplType);

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
    /// Gets the constructor parameter type full names of an open generic type.
    /// </summary>
    private static ImmutableArray<string> GetOpenGenericConstructorParameters(
        INamedTypeSymbol openGenericType
    )
    {
        var constructors = openGenericType
            .Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();

        var constructor = constructors.FirstOrDefault();
        if (constructor is null)
            return ImmutableArray<string>.Empty;

        return
        [
            .. constructor.Parameters.Select(p =>
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            )
        ];
    }

    /// <summary>
    /// Analyze a closed generic usage.
    /// </summary>
    private static ClosedGenericUsage? AnalyzeClosedGenericUsage(ITypeSymbol closedType)
    {
        if (closedType is not INamedTypeSymbol { IsGenericType: true } namedType)
            return null;

        // Skip if any type argument is a type parameter (e.g., T in ILog<T>)
        // We only want fully closed generics like IRepository<User>
        if (namedType.TypeArguments.Any(ta => ta is ITypeParameterSymbol))
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
        List<ClosedGenericUsage> closedUsages
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
                .Select(typeFullName => SubstituteTypeParameters(typeFullName, typeParamMap))
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
            switch (c)
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(typeArgsStr.Substring(start, i - start).Trim());
                    start = i + 1;
                    break;
            }
        }

        if (start < typeArgsStr.Length)
            result.Add(typeArgsStr.Substring(start).Trim());

        return result;
    }

    private static ServiceRegistration? AnalyzeInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel
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

        if (methodName is null || !PicoDiNames.RegisterMethodNames.Contains(methodName))
            return null;

        // Check if this is a Pico.DI registration method
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Verify this is a Pico.DI registration method
        // C# 14 extension types may have containing types like "__extension__ISvcContainer" or similar
        var containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        var containingNs = methodSymbol.ContainingNamespace?.ToDisplayString() ?? "";

        // Also check receiver type for extension methods
        var receiverType = methodSymbol.ReceiverType?.ToDisplayString() ?? "";
        var reducedFrom = methodSymbol.ReducedFrom?.ContainingNamespace?.ToDisplayString() ?? "";

        var isPicoDiMethod =
            containingType.StartsWith(PicoDiNames.RootNamespace)
            || containingType.Contains(PicoDiNames.SvcContainer)
            || containingType.Contains(PicoDiNames.ISvcContainer)
            || containingNs.StartsWith(PicoDiNames.RootNamespace)
            || receiverType.Contains(PicoDiNames.ISvcContainer)
            || receiverType.Contains(PicoDiNames.SvcContainer)
            || reducedFrom.StartsWith(PicoDiNames.RootNamespace);

        if (!isPicoDiMethod)
            return null;

        // Extract type arguments
        ITypeSymbol? serviceType = null;
        ITypeSymbol? implementationType = null;
        var lifetime = PicoDiNames.Singleton;
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
            if (argType is INamedTypeSymbol { Name: PicoDiNames.Func })
            {
                hasFactory = true;
            }
            else
                switch (argType?.Name)
                {
                    // Check if it's SvcLifetime enum
                    case PicoDiNames.SvcLifetime:
                        lifetime = arg.Expression.ToString() switch
                        {
                            var s when s.Contains(PicoDiNames.Transient) => PicoDiNames.Transient,
                            var s when s.Contains(PicoDiNames.Scoped) => PicoDiNames.Scoped,
                            _ => PicoDiNames.Singleton
                        };
                        break;
                    // Check if it's a Type argument (for non-generic overloads)
                    case PicoDiNames.Type when arg.Expression is TypeOfExpressionSyntax typeOfExpr:
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
        if (methodName.Contains(PicoDiNames.Transient))
            lifetime = PicoDiNames.Transient;
        else if (methodName.Contains(PicoDiNames.Scoped))
            lifetime = PicoDiNames.Scoped;
        else if (methodName.Contains(PicoDiNames.Singleton))
            lifetime = PicoDiNames.Singleton;

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

    private static ImmutableArray<string> GetConstructorParameters(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return ImmutableArray<string>.Empty;

        // Find the best constructor (prefer the one with most parameters, or [ActivatorUtilitiesConstructor] if present)
        var constructors = namedType
            .Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();

        var constructor = constructors.FirstOrDefault();
        if (constructor is null)
            return ImmutableArray<string>.Empty;

        return
        [
            .. constructor.Parameters.Select(p =>
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            )
        ];
    }

    private static string GenerateSource(List<ServiceRegistration> registrations, Compilation compilation)
    {
        var sb = new StringBuilder();

        // Generate unique class name based on assembly name to avoid conflicts
        var assemblyName = compilation.AssemblyName ?? "Unknown";
        var safeClassName = "GeneratedServiceRegistrations_" +
            new string(assemblyName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Pico.DI.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Auto-generated service registrations with AOT-compatible factory methods."
        );
        sb.AppendLine("/// Optimized with inlined resolution chains for Transient services.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {safeClassName}");
        sb.AppendLine("{");

        // Generate Module Initializer to auto-register the configurator
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Module initializer that automatically registers the service configuration."
        );
        sb.AppendLine(
            "    /// This is called automatically when the assembly is loaded, before Main() runs."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void AutoRegisterConfigurator()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        global::Pico.DI.Abs.SvcContainerAutoConfiguration.RegisterConfigurator("
        );
        sb.AppendLine(
            "            static container => ConfigureGeneratedServicesCore(container));"
        );
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Registers all scanned services with pre-compiled factory methods and calls Build() to optimize."
        );
        sb.AppendLine(
            "    /// This method is auto-generated by scanning Register* method calls in the codebase."
        );
        sb.AppendLine(
            "    /// Note: Services are now auto-registered via Module Initializer when SvcContainer is created."
        );
        sb.AppendLine(
            "    /// Call this method manually only if you want to explicitly trigger Build() optimization."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static global::Pico.DI.Abs.ISvcContainer ConfigureGeneratedServices(this global::Pico.DI.Abs.ISvcContainer container)"
        );
        sb.AppendLine("    {");
        sb.AppendLine("        ConfigureGeneratedServicesCore(container);");
        sb.AppendLine(
            "        if (container is global::Pico.DI.SvcContainer svcContainer) svcContainer.Build();"
        );
        sb.AppendLine("        return container;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Core implementation that registers services without calling Build()."
        );
        sb.AppendLine(
            "    /// Used by Module Initializer to allow additional registrations after auto-configuration."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    private static void ConfigureGeneratedServicesCore(global::Pico.DI.Abs.ISvcContainer container)"
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
                var factoryCode = GenerateInlinedFactory(reg, registrationLookup, [], 0);
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
                    var factoryCode = GenerateInlinedFactory(reg, registrationLookup, [], 4);
                    sb.AppendLine($"                {factoryCode}{comma}");
                }
            }

            sb.AppendLine($"            }},");
            sb.AppendLine($"            global::Pico.DI.Abs.SvcLifetime.Transient));");
            sb.AppendLine();
        }

        // Close ConfigureGeneratedServicesCore method
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate typed resolver extension methods for each service (Optimization #9)
        GenerateTypedResolvers(sb, registrations, registrationLookup);

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates typed resolver extension methods that bypass dictionary lookup.
    /// These provide direct factory calls for maximum performance.
    /// </summary>
    private static void GenerateTypedResolvers(
        StringBuilder sb,
        List<ServiceRegistration> registrations,
        Dictionary<string, ServiceRegistration> registrationLookup
    )
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// High-performance typed resolvers that bypass dictionary lookup.");
        sb.AppendLine("    /// Use these methods directly for maximum resolution speed.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static class Resolve");
        sb.AppendLine("    {");

        // Track generated method names to avoid duplicates
        var generatedMethods = new HashSet<string>();

        foreach (var reg in registrations)
        {
            // Generate a unique method name from service type
            var methodName = GetResolverMethodName(reg.ServiceTypeFullName);
            if (!generatedMethods.Add(methodName))
                continue; // Skip duplicates

            var serviceType = reg.ServiceTypeFullName;

            sb.AppendLine(
                $"        /// <summary>Resolves {reg.ServiceTypeName} with direct factory call.</summary>"
            );
            sb.AppendLine(
                $"        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]"
            );
            sb.AppendLine(
                $"        public static {serviceType} {methodName}(global::Pico.DI.Abs.ISvcScope scope)"
            );
            sb.AppendLine("        {");

            // Generate resolution based on lifetime
            switch (reg.Lifetime)
            {
                case PicoDiNames.Transient:
                    // Direct inline construction for transient
                    var transientFactory = GenerateInlinedFactory(reg, registrationLookup, [], 0, "scope");
                    sb.AppendLine($"            return {transientFactory};");
                    break;

                case PicoDiNames.Singleton:
                    // Use cache helper for singleton - use "s" as scope var name for static lambda
                    sb.AppendLine(
                        $"            return SingletonCache<{serviceType}>.GetOrCreate(scope, static s =>"
                    );
                    var singletonFactory = GenerateInlinedFactory(reg, registrationLookup, [], 0, "s");
                    sb.AppendLine($"                {singletonFactory});");
                    break;

                case PicoDiNames.Scoped:
                    // Use scope's GetService for scoped (needs scope-level caching)
                    sb.AppendLine(
                        $"            return ({serviceType})scope.GetService(typeof({serviceType}));"
                    );
                    break;
            }

            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate singleton cache helper class
        sb.AppendLine(
            "    /// <summary>Thread-safe singleton cache using static generic class pattern.</summary>"
        );
        sb.AppendLine("    private static class SingletonCache<T> where T : class");
        sb.AppendLine("    {");
        sb.AppendLine("        private static T? _instance;");
        sb.AppendLine("        private static object? _lock;");
        sb.AppendLine();
        sb.AppendLine(
            "        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]"
        );
        sb.AppendLine(
            "        public static T GetOrCreate(global::Pico.DI.Abs.ISvcScope scope, global::System.Func<global::Pico.DI.Abs.ISvcScope, T> factory)"
        );
        sb.AppendLine("        {");
        sb.AppendLine(
            "            var instance = global::System.Threading.Volatile.Read(ref _instance);"
        );
        sb.AppendLine("            if (instance != null) return instance;");
        sb.AppendLine("            return GetOrCreateSlow(scope, factory);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine(
            "        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]"
        );
        sb.AppendLine(
            "        private static T GetOrCreateSlow(global::Pico.DI.Abs.ISvcScope scope, global::System.Func<global::Pico.DI.Abs.ISvcScope, T> factory)"
        );
        sb.AppendLine("        {");
        sb.AppendLine("            _lock ??= new object();");
        sb.AppendLine("            lock (_lock)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                var instance = global::System.Threading.Volatile.Read(ref _instance);"
        );
        sb.AppendLine("                if (instance != null) return instance;");
        sb.AppendLine("                instance = factory(scope);");
        sb.AppendLine(
            "                global::System.Threading.Volatile.Write(ref _instance, instance);"
        );
        sb.AppendLine("                return instance;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Gets a valid C# method name from a service type full name.
    /// </summary>
    private static string GetResolverMethodName(string serviceTypeFullName)
    {
        // Remove global:: prefix
        var name = serviceTypeFullName.Replace("global::", "");

        // Handle generic types: ILogger<UserService> -> ILogger_UserService
        name = name.Replace("<", "_").Replace(">", "").Replace(",", "_").Replace(" ", "");

        // Replace dots with underscores for namespaces
        name = name.Replace(".", "_");

        return name;
    }

    /// <summary>
    /// Generates an inlined factory expression for the given registration.
    /// For Transient dependencies, recursively inlines the construction.
    /// For Singleton/Scoped dependencies, calls scope.GetService().
    /// </summary>
    /// <param name="scopeVarName">The variable name for the scope parameter (e.g., "scope" or "s").</param>
    private static string GenerateInlinedFactory(
        ServiceRegistration reg,
        Dictionary<string, ServiceRegistration> registrationLookup,
        HashSet<string> visitedTypes,
        int indentLevel,
        string scopeVarName = "scope"
    )
    {
        if (reg.ConstructorParameters.IsEmpty)
        {
            return $"new {reg.ImplementationTypeFullName}()";
        }

        var paramExpressions = reg.ConstructorParameters
            .Select(
                paramTypeFullName =>
                    GenerateParameterExpression(
                        paramTypeFullName,
                        registrationLookup,
                        visitedTypes,
                        indentLevel + 1,
                        scopeVarName
                    )
            )
            .ToList();

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
    /// <param name="scopeVarName">The variable name for the scope parameter (e.g., "scope" or "s").</param>
    private static string GenerateParameterExpression(
        string paramTypeFullName,
        Dictionary<string, ServiceRegistration> registrationLookup,
        HashSet<string> visitedTypes,
        int indentLevel,
        string scopeVarName = "scope"
    )
    {
        // Check if we have a registration for this type
        if (!registrationLookup.TryGetValue(paramTypeFullName, out var depReg))
            return $"({paramTypeFullName}){scopeVarName}.GetService(typeof({paramTypeFullName}))";
        // Only inline Transient dependencies to avoid breaking singleton/scoped semantics
        if (depReg.Lifetime != PicoDiNames.Transient)
            return $"({paramTypeFullName}){scopeVarName}.GetService(typeof({paramTypeFullName}))";
        // Check for circular dependency
        if (visitedTypes.Contains(paramTypeFullName))
        {
            // Fall back to GetService for circular references
            return $"({paramTypeFullName}){scopeVarName}.GetService(typeof({paramTypeFullName}))";
        }

        // Mark as visited and recursively inline
        var newVisited = new HashSet<string>(visitedTypes) { paramTypeFullName };
        return GenerateInlinedFactory(depReg, registrationLookup, newVisited, indentLevel, scopeVarName);

        // For Singleton, Scoped, or unknown dependencies, use GetService
    }
}
