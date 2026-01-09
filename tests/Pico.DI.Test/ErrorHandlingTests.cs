namespace Pico.DI.Test;

/// <summary>
/// Tests for error handling and edge cases.
/// </summary>
public class ErrorHandlingTests
{
    #region Unregistered Service Tests

    [Test]
    public async Task GetService_UnregisteredService_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        using var scope = container.CreateScope();

        // Act & Assert
        await Assert.That(() => scope.GetService<ISimpleService>()).Throws<Exception>(); // PicoDiException or KeyNotFoundException
    }

    [Test]
    public async Task GetServices_UnregisteredService_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        using var scope = container.CreateScope();

        // Act & Assert - GetServices also throws for unregistered service
        await Assert
            .That(() => scope.GetServices<ISimpleService>().ToList())
            .Throws<PicoDiException>();
    }

    #endregion

    #region Factory Exception Tests

    [Test]
    public async Task GetService_FactoryThrows_ExceptionPropagates()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ =>
            throw new InvalidOperationException("Factory failed")
        );
        using var scope = container.CreateScope();

        // Act & Assert
        await Assert
            .That(() => scope.GetService<ISimpleService>())
            .Throws<InvalidOperationException>()
            .WithMessage("Factory failed");
    }

    [Test]
    public async Task GetService_SingletonFactoryThrows_SameExceptionOnRetry()
    {
        // Arrange
        var callCount = 0;
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<ISimpleService>(_ =>
        {
            callCount++;
            throw new InvalidOperationException($"Factory failed attempt {callCount}");
        });
        using var scope = container.CreateScope();

        // Act & Assert - Each retry calls the factory again
        await Assert
            .That(() => scope.GetService<ISimpleService>())
            .Throws<InvalidOperationException>();

        await Assert
            .That(() => scope.GetService<ISimpleService>())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetService_ScopedFactoryThrows_ExceptionOnEachScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ =>
            throw new InvalidOperationException("Scoped factory failed")
        );

        using var scope1 = container.CreateScope();
        using var scope2 = container.CreateScope();

        // Act & Assert - Both scopes should fail
        await Assert
            .That(() => scope1.GetService<ISimpleService>())
            .Throws<InvalidOperationException>();

        await Assert
            .That(() => scope2.GetService<ISimpleService>())
            .Throws<InvalidOperationException>();
    }

    #endregion

    #region Null Handling Tests

    [Test]
    public async Task Register_NullDescriptor_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert.That(() => container.Register(null!)).Throws<Exception>(); // ArgumentNullException or NullReferenceException
    }

    [Test]
    public async Task Register_NullFactory_ThrowsException()
    {
        // Act & Assert
        await Assert
            .That(() => new SvcDescriptor(typeof(ISimpleService), (Func<ISvcScope, object>)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task RegisterSingle_NullInstance_ThrowsException()
    {
        // Act & Assert
        await Assert
            .That(() => new SvcDescriptor(typeof(ISimpleService), (object)null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region Type Placeholder Registration Tests

    [Test]
    public async Task RegisterPlaceholder_WithoutSourceGenerator_ThrowsOnResolve()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // These are placeholder methods that return container immediately
        // Without source generator, no actual registration happens
        container.RegisterTransient<ISimpleService>();
        using var scope = container.CreateScope();

        // Act & Assert - Service was never actually registered
        await Assert.That(() => scope.GetService<ISimpleService>()).Throws<Exception>();
    }

    [Test]
    public async Task RegisterOpenGeneric_NonGenericType_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert - Non-generic types should throw
        await Assert
            .That(() => container.RegisterTransient(typeof(ISimpleService), typeof(SimpleService)))
            .Throws<SourceGeneratorRequiredException>();
    }

    #endregion

    #region Disposed Scope Tests

    [Test]
    public async Task DisposedScope_GetService_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        await Assert
            .That(() => scope.GetService<ISimpleService>())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DisposedScope_CreateChildScope_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var scope = container.CreateScope();
        scope.Dispose();

        // Act & Assert
        await Assert.That(() => scope.CreateScope()).Throws<ObjectDisposedException>();
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public async Task EmptyContainer_Build_NoError()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert - Should not throw
        container.Build();
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task EmptyContainer_CreateScope_NoError()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert - Should not throw
        using var scope = container.CreateScope();
        await Assert.That(scope).IsNotNull();
    }

    [Test]
    public async Task RegisterSameServiceMultipleTimes_LastWins()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IConfigurableService>(static _ => new ConfigurableService(
            "first"
        ));
        container.RegisterTransient<IConfigurableService>(static _ => new ConfigurableService(
            "second"
        ));
        container.RegisterTransient<IConfigurableService>(static _ => new ConfigurableService(
            "third"
        ));
        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IConfigurableService>();

        // Assert - GetService returns the last registered
        await Assert.That(service.Configuration).IsEqualTo("third");
    }

    [Test]
    public async Task RegisterSameServiceMultipleTimes_GetServicesReturnsAll()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IConfigurableService>(static _ => new ConfigurableService(
            "first"
        ));
        container.RegisterTransient<IConfigurableService>(static _ => new ConfigurableService(
            "second"
        ));
        container.RegisterTransient<IConfigurableService>(static _ => new ConfigurableService(
            "third"
        ));
        using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<IConfigurableService>().ToList();

        // Assert
        await Assert.That(services.Count).IsEqualTo(3);
        await Assert
            .That(services.Select(s => s.Configuration))
            .IsEquivalentTo(new[] { "first", "second", "third" });
    }

    #endregion
}
