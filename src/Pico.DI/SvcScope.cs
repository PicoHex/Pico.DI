namespace Pico.DI;

public sealed class SvcScope(ConcurrentDictionary<Type, List<SvcDescriptor>> descriptorCache)
    : ISvcScope
{
    private readonly ConcurrentDictionary<SvcDescriptor, object> _scopedInstances = new();
    private readonly ConcurrentDictionary<SvcDescriptor, Lock> _singletonLocks = new();
    private readonly AsyncLocal<HashSet<Type>> _resolutionStack = new();
    private bool _disposed;

    public ISvcScope CreateScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new SvcScope(descriptorCache);
    }

    [RequiresDynamicCode("IEnumerable<T> and open generic resolution require dynamic code.")]
    [RequiresUnreferencedCode("Open generic resolution requires reflection.")]
    public object GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Handle IEnumerable<T> auto-injection
        if (
            serviceType.IsGenericType
            && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        )
        {
            var elementType = serviceType.GetGenericArguments()[0];
            return GetServicesAsTypedEnumerable(elementType);
        }

        // Circular dependency detection
        var stack = _resolutionStack.Value ??= [];
        if (!stack.Add(serviceType))
        {
            var chain = string.Join(" -> ", stack.Select(t => t.Name)) + " -> " + serviceType.Name;
            throw new PicoDiException($"Circular dependency detected: {chain}");
        }

        try
        {
            // Try open generic resolution first
            if (serviceType.IsGenericType && !descriptorCache.ContainsKey(serviceType))
            {
                var openGenericResult = TryResolveOpenGeneric(serviceType);
                if (openGenericResult != null)
                    return openGenericResult;
            }

            if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
                throw new PicoDiException(
                    $"Service type '{serviceType.FullName}' is not registered."
                );
            var resolver =
                resolvers.LastOrDefault()
                ?? throw new PicoDiException(
                    $"No service descriptor found for type '{serviceType.FullName}'."
                );
            return resolver.Lifetime switch
            {
                SvcLifetime.Transient
                    => resolver.Factory != null
                        ? resolver.Factory(this)
                        : throw new PicoDiException(
                            $"No factory registered for transient service '{serviceType.FullName}'."
                        ),
                SvcLifetime.Singleton => GetOrCreateSingleton(serviceType, resolver),
                SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
                _
                    => throw new ArgumentOutOfRangeException(
                        nameof(resolver.Lifetime),
                        resolver.Lifetime,
                        $"Unknown service lifetime '{resolver.Lifetime}'."
                    )
            };
        }
        finally
        {
            stack.Remove(serviceType);
        }
    }

    /// <summary>
    /// Returns services as a properly typed IEnumerable&lt;T&gt; for injection.
    /// Note: This method requires dynamic code and may not work in AOT scenarios.
    /// </summary>
    [RequiresDynamicCode("Creating typed arrays requires dynamic code generation.")]
    private object GetServicesAsTypedEnumerable(Type elementType)
    {
        if (!descriptorCache.TryGetValue(elementType, out var resolvers))
        {
            // Return empty typed array if not registered
            return Array.CreateInstance(elementType, 0);
        }

        var services = resolvers
            .Select(resolver =>
                resolver.Lifetime switch
                {
                    SvcLifetime.Transient
                        => resolver.Factory != null
                            ? resolver.Factory(this)
                            : throw new PicoDiException(
                                $"No factory registered for transient service '{elementType.FullName}'."
                            ),
                    SvcLifetime.Singleton => GetOrCreateSingleton(elementType, resolver),
                    SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
                    _
                        => throw new ArgumentOutOfRangeException(
                            nameof(resolver.Lifetime),
                            resolver.Lifetime,
                            $"Unknown service lifetime '{resolver.Lifetime}'."
                        )
                }
            )
            .ToArray();

        // Create typed array
        var typedArray = Array.CreateInstance(elementType, services.Length);
        for (var i = 0; i < services.Length; i++)
        {
            typedArray.SetValue(services[i], i);
        }
        return typedArray;
    }

    /// <summary>
    /// Tries to resolve an open generic type (e.g., IRepository&lt;T&gt; -&gt; Repository&lt;T&gt;).
    /// Note: This method requires dynamic code and may not work in AOT scenarios.
    /// </summary>
    [RequiresDynamicCode("Open generic resolution requires runtime type generation.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL2055",
        Justification = "Open generics require MakeGenericType at runtime."
    )]
    private object? TryResolveOpenGeneric(Type closedGenericType)
    {
        var openGenericType = closedGenericType.GetGenericTypeDefinition();

        if (!descriptorCache.TryGetValue(openGenericType, out var resolvers))
            return null;

        var resolver = resolvers.LastOrDefault();
        if (resolver?.ImplementationType == null)
            return null;

        // Check if implementation type is also an open generic
        if (!resolver.ImplementationType.IsGenericTypeDefinition)
            return null;

        // Build closed implementation type
        var typeArguments = closedGenericType.GetGenericArguments();
        var closedImplementationType = resolver.ImplementationType.MakeGenericType(typeArguments);

        // Create instance using reflection
        var instance = CreateInstanceWithInjection(
            closedImplementationType,
            resolver.Lifetime,
            null
        );

        // Create descriptor with factory for future requests
        Func<ISvcScope, object> factory = scope =>
            ((SvcScope)scope).CreateInstanceWithInjection(
                closedImplementationType,
                resolver.Lifetime,
                null
            );

        var closedDescriptor = new SvcDescriptor(closedGenericType, factory, resolver.Lifetime);

        // Cache singleton instance if applicable
        if (resolver.Lifetime == SvcLifetime.Singleton)
        {
            closedDescriptor.SingleInstance = instance;
        }

        // Register the closed type for future requests
        descriptorCache.AddOrUpdate(
            closedGenericType,
            _ => [closedDescriptor],
            (_, list) =>
            {
                list.Add(closedDescriptor);
                return list;
            }
        );

        // For scoped, cache in this scope
        if (resolver.Lifetime == SvcLifetime.Scoped)
        {
            _scopedInstances.TryAdd(closedDescriptor, instance);
        }

        return instance;
    }

    /// <summary>
    /// Creates an instance of the specified type, injecting constructor dependencies.
    /// Used for open generic resolution where no factory is available.
    /// Note: This method uses reflection and may not work in AOT scenarios.
    /// </summary>
    [RequiresUnreferencedCode("Reflection-based instantiation may not work with trimming.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL2070",
        Justification = "Open generics require reflection for instantiation."
    )]
    private object CreateInstanceWithInjection(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType,
        SvcLifetime lifetime,
        SvcDescriptor? descriptor
    )
    {
        var constructors = implementationType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .ToArray();

        if (constructors.Length == 0)
            throw new PicoDiException(
                $"No public constructor found for type '{implementationType.FullName}'."
            );

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        var args = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            args[i] = GetService(parameters[i].ParameterType);
        }

        var instance = constructor.Invoke(args);

        // Handle lifetime caching only if descriptor is provided
        if (descriptor != null)
        {
            switch (lifetime)
            {
                case SvcLifetime.Singleton:
                    descriptor.SingleInstance = instance;
                    break;
                case SvcLifetime.Scoped:
                    _scopedInstances.TryAdd(descriptor, instance);
                    break;
            }
        }

        return instance;
    }

    private object GetOrCreateSingleton(Type serviceType, SvcDescriptor resolver)
    {
        if (resolver.SingleInstance != null)
            return resolver.SingleInstance;

        var singletonLock = _singletonLocks.GetOrAdd(resolver, _ => new Lock());
        lock (singletonLock)
        {
            if (resolver.SingleInstance != null)
                return resolver.SingleInstance;

            resolver.SingleInstance =
                resolver.Factory != null
                    ? resolver.Factory(this)
                    : throw new PicoDiException(
                        $"No factory or instance registered for singleton service '{serviceType.FullName}'."
                    );

            return resolver.SingleInstance;
        }
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!descriptorCache.TryGetValue(serviceType, out var resolvers))
            throw new PicoDiException($"Service type '{serviceType.FullName}' is not registered.");

        return resolvers.Select(resolver =>
            resolver.Lifetime switch
            {
                SvcLifetime.Transient
                    => resolver.Factory != null
                        ? resolver.Factory(this)
                        : throw new PicoDiException(
                            $"No factory registered for transient service '{serviceType.FullName}'."
                        ),
                SvcLifetime.Singleton => GetOrCreateSingleton(serviceType, resolver),
                SvcLifetime.Scoped => GetOrAddScopedInstance(resolver),
                _
                    => throw new ArgumentOutOfRangeException(
                        nameof(resolver.Lifetime),
                        resolver.Lifetime,
                        $"Unknown service lifetime '{resolver.Lifetime}'."
                    )
            }
        );
    }

    private object GetOrAddScopedInstance(SvcDescriptor resolver) =>
        _scopedInstances.GetOrAdd(
            resolver,
            desc =>
                desc.Factory != null
                    ? desc.Factory(this)
                    : throw new PicoDiException(
                        $"No factory registered for scoped service '{desc.ServiceType.FullName}'."
                    )
        );

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            foreach (var svc in _scopedInstances.Values)
            {
                if (svc is IDisposable disposable)
                    disposable.Dispose();
            }
            _scopedInstances.Clear();
        }
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        foreach (var svc in _scopedInstances.Values)
        {
            switch (svc)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
        _scopedInstances.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
