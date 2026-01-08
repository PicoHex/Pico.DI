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

    // External service patterns (cannot use nameof - external dependencies)
    public static readonly string[] ServiceAssociatedGenericPatterns =
    [
        "ILogger<>",
        "Logger<>",
        "IOptions<>",
        "IOptionsSnapshot<>",
        "IOptionsMonitor<>",
        "IConfiguration<>",
        "Lazy<>",
        "IValidator<>",
        "Validator<>"
    ];
}
