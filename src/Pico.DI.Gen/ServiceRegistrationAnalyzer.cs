namespace Pico.DI.Gen;

/// <summary>
/// Diagnostic analyzer that detects potential issues with service registrations at compile time.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ServiceRegistrationAnalyzer : DiagnosticAnalyzer
{
    // Diagnostic IDs
    public const string UnregisteredDependencyId = "PICO001";
    public const string CircularDependencyId = "PICO002";
    public const string AbstractTypeRegistrationId = "PICO003";
    public const string MissingPublicConstructorId = "PICO004";

    private static readonly DiagnosticDescriptor UnregisteredDependencyRule =
        new(
            UnregisteredDependencyId,
            "Unregistered dependency",
            "Constructor parameter '{0}' of type '{1}' may not be registered in the container",
            "Pico.DI",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The constructor has a dependency that might not be registered. Consider registering this type."
        );

    private static readonly DiagnosticDescriptor CircularDependencyRule =
        new(
            CircularDependencyId,
            "Potential circular dependency",
            "Potential circular dependency detected: {0}",
            "Pico.DI",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "A circular dependency chain was detected which will cause a runtime exception."
        );

    private static readonly DiagnosticDescriptor AbstractTypeRegistrationRule =
        new(
            AbstractTypeRegistrationId,
            "Abstract type registration",
            "Cannot register abstract type or interface '{0}' as implementation",
            "Pico.DI",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Abstract types and interfaces cannot be instantiated. Provide a concrete implementation type."
        );

    private static readonly DiagnosticDescriptor MissingPublicConstructorRule =
        new(
            MissingPublicConstructorId,
            "Missing public constructor",
            "Type '{0}' has no public constructor",
            "Pico.DI",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The implementation type must have at least one public constructor for dependency injection."
        );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>

        [
            UnregisteredDependencyRule,
            CircularDependencyRule,
            AbstractTypeRegistrationRule,
            MissingPublicConstructorRule
        ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get method name
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        if (methodName == null || !IsRegisterMethod(methodName))
            return;

        // Get semantic info
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Verify this is a Pico.DI method
        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (
            containingType == null
            || (!containingType.StartsWith("Pico.DI") && !containingType.Contains("SvcContainer"))
        )
            return;

        // Check if it's a factory-based registration - multiple detection methods
        // 1. Check if any argument is a lambda expression or anonymous method
        var hasLambda = invocation
            .ArgumentList
            .Arguments
            .Any(
                arg =>
                    arg.Expression is LambdaExpressionSyntax
                    || arg.Expression is AnonymousMethodExpressionSyntax
                    || arg.Expression is AnonymousFunctionExpressionSyntax
            );

        if (hasLambda)
            return;

        // 2. Check if the method has a Func parameter (covers delegate and method group cases)
        var hasFactoryParameter = methodSymbol
            .Parameters
            .Any(p => p.Type is INamedTypeSymbol { Name: "Func" });

        if (hasFactoryParameter && invocation.ArgumentList.Arguments.Count > 0)
            return;

        // Extract type arguments
        var genericNameSyntax = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax gn } => gn,
            GenericNameSyntax gn => gn,
            _ => null
        };

        if (genericNameSyntax?.TypeArgumentList.Arguments.Count is null or 0)
            return;
        var typeArgs = genericNameSyntax.TypeArgumentList.Arguments;

        // Skip placeholder methods (type-based registration scanned by Source Generator)
        // These are methods with only type arguments and possibly Type/SvcLifetime arguments
        // but no factory. They return container immediately and real registration is generated.
        // We only want to analyze Register<TService, TImplementation>() where both types are concrete.
        if (typeArgs.Count == 1)
        {
            // Self-registration like Register<TService>() - skip unless it has a concrete type
            // These are placeholder methods for Source Generator
            return;
        }

        // For Register<TService, TImplementation>() - check the implementation type (last type arg)
        var implementationTypeArg = typeArgs.Last();
        var implementationType = context.SemanticModel.GetTypeInfo(implementationTypeArg).Type;

        if (implementationType == null)
            return;
        // Check if implementation is abstract or interface
        if (implementationType.TypeKind == TypeKind.Interface || implementationType.IsAbstract)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    AbstractTypeRegistrationRule,
                    implementationTypeArg.GetLocation(),
                    implementationType.Name
                )
            );
            return;
        }

        // Check for public constructor
        if (implementationType is not INamedTypeSymbol namedType)
            return;
        var hasPublicConstructor = namedType
            .Constructors
            .Any(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public);

        if (!hasPublicConstructor && !implementationType.IsValueType)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    MissingPublicConstructorRule,
                    implementationTypeArg.GetLocation(),
                    implementationType.Name
                )
            );
        }
    }

    private static bool IsRegisterMethod(string methodName)
    {
        return methodName
            is "Register"
                or "RegisterTransient"
                or "RegisterScoped"
                or "RegisterSingleton";
    }
}
