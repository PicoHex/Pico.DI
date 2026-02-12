namespace Pico.DI.Gen.Constants;

/// <summary>
/// Centralized constants for Pico.DI type and method names.
/// </summary>
internal static class PicoDiNames
{
    // Namespace
    public const string RootNamespace = "Pico.DI";

    // Type names
    public const string SvcContainer = "SvcContainer";
    public const string ISvcContainer = "ISvcContainer";
    public const string SvcLifetime = "SvcLifetime";
    public const string Func = nameof(Func);
    public const string Type = nameof(System.Type);

    // Method names
    public const string Register = "Register";
    public const string RegisterTransient = "RegisterTransient";
    public const string RegisterScoped = "RegisterScoped";
    public const string RegisterSingleton = "RegisterSingleton";
    public const string GetService = "GetService";
    public const string GetServices = "GetServices";

    // Lifetime keywords
    public const string Transient = "Transient";
    public const string Scoped = "Scoped";
    public const string Singleton = "Singleton";

    // Namespace prefixes
    public const string SystemNamespace = nameof(System);
    public const string GlobalPrefix = "global::";
    public const string GlobalSystemPrefix = $"{GlobalPrefix}{nameof(System)}";

    // Method name collection
    public static readonly string[] RegisterMethodNames =
    [
        Register,
        RegisterTransient,
        RegisterScoped,
        RegisterSingleton
    ];

    /// <summary>
    /// Determines whether the given method symbol belongs to Pico.DI.
    /// Handles C# 14 extension types, reduced extension methods, and receiver types.
    /// </summary>
    public static bool IsPicoDiMethod(IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType?.ToDisplayString() ?? "";
        var containingNs = methodSymbol.ContainingNamespace?.ToDisplayString() ?? "";
        var receiverType = methodSymbol.ReceiverType?.ToDisplayString() ?? "";
        var reducedFrom = methodSymbol.ReducedFrom?.ContainingNamespace?.ToDisplayString() ?? "";

        return containingType.StartsWith(RootNamespace)
            || containingType.Contains(SvcContainer)
            || containingType.Contains(ISvcContainer)
            || containingNs.StartsWith(RootNamespace)
            || receiverType.Contains(ISvcContainer)
            || receiverType.Contains(SvcContainer)
            || reducedFrom.StartsWith(RootNamespace);
    }

    /// <summary>
    /// Infers lifetime from a Register* method name (e.g., RegisterTransient → Transient).
    /// Returns <paramref name="fallback"/> if no lifetime keyword is found.
    /// </summary>
    public static string InferLifetimeFromMethodName(string methodName, string fallback = Singleton)
    {
        if (methodName.Contains(Transient))
            return Transient;
        if (methodName.Contains(Scoped))
            return Scoped;
        if (methodName.Contains(Singleton))
            return Singleton;
        return fallback;
    }

    /// <summary>
    /// Parses a SvcLifetime value from its expression text (e.g., "SvcLifetime.Transient").
    /// </summary>
    public static string ParseLifetimeFromExpression(string expressionText)
    {
        if (expressionText.Contains(Transient))
            return Transient;
        if (expressionText.Contains(Scoped))
            return Scoped;
        return Singleton;
    }
}
