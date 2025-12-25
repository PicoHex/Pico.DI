namespace Pico.DI.Abs;

/// <summary>
/// Metadata for a decorator generic type that wraps service instances.
/// Enables AOT-compatible decorator pattern without runtime reflection.
///
/// Example:
/// <code>
/// // Register Logger<T> as a decorator for any registered service type T
/// container.RegisterDecorator<Logger<>>();
///
/// // Then when resolving Logger<IUser>, the source generator will create:
/// // Logger<IUser> wrapping the registered IUser service
/// </code>
/// </summary>
public sealed class DecoratorMetadata
{
    /// <summary>
    /// Gets the open generic decorator type (e.g., Logger<>).
    /// </summary>
    public Type DecoratorType { get; }

    /// <summary>
    /// Gets the lifetime for created decorator instances.
    /// Typically Transient, but can be Scoped or Singleton.
    /// </summary>
    public SvcLifetime Lifetime { get; }

    /// <summary>
    /// Gets the index of the constructor parameter that receives the decorated service.
    /// The source generator uses this to know which parameter gets the inner service.
    /// Default is 0 (first parameter).
    /// </summary>
    public int DecoratedServiceParameterIndex { get; }

    /// <summary>
    /// Creates a new decorator metadata.
    /// </summary>
    /// <param name="decoratorType">The open generic decorator type (e.g., Logger<>).</param>
    /// <param name="lifetime">The lifetime for decorator instances.</param>
    /// <param name="decoratedServiceParameterIndex">
    /// The constructor parameter index that receives the decorated service.
    /// Set to -1 if the decorator automatically detects the parameter by type matching.
    /// </param>
    public DecoratorMetadata(
        Type decoratorType,
        SvcLifetime lifetime = SvcLifetime.Transient,
        int decoratedServiceParameterIndex = 0
    )
    {
        if (decoratorType == null)
            throw new ArgumentNullException(nameof(decoratorType));

        if (!decoratorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"Decorator type '{decoratorType.FullName}' must be an open generic type (e.g., Logger<>)",
                nameof(decoratorType)
            );

        DecoratorType = decoratorType;
        Lifetime = lifetime;
        DecoratedServiceParameterIndex = decoratedServiceParameterIndex;
    }
}
