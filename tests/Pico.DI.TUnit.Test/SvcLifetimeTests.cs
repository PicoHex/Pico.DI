namespace Pico.DI.TUnit.Test;

/// <summary>
/// Tests for SvcLifetime enum.
/// </summary>
public class SvcLifetimeTests : TUnitTestBase
{
    [Test]
    public async Task SvcLifetime_HasThreeValues()
    {
        // Act
        var values = Enum.GetValues<SvcLifetime>();

        // Assert
        await Assert.That(values.Length).IsEqualTo(3);
    }

    [Test]
    public async Task SvcLifetime_ContainsTransient()
    {
        // Assert
        await Assert.That(Enum.IsDefined(SvcLifetime.Transient)).IsTrue();
    }

    [Test]
    public async Task SvcLifetime_ContainsSingleton()
    {
        // Assert
        await Assert.That(Enum.IsDefined(SvcLifetime.Singleton)).IsTrue();
    }

    [Test]
    public async Task SvcLifetime_ContainsScoped()
    {
        // Assert
        await Assert.That(Enum.IsDefined(SvcLifetime.Scoped)).IsTrue();
    }

    [Test]
    public async Task SvcLifetime_TransientValue_IsZero()
    {
        // Arrange
        var transientValue = (byte)SvcLifetime.Transient;

        // Assert
        await Assert.That(transientValue).IsEqualTo((byte)0);
    }

    [Test]
    public async Task SvcLifetime_IsByteEnum()
    {
        // Assert
        await Assert.That(Enum.GetUnderlyingType(typeof(SvcLifetime))).IsEqualTo(typeof(byte));
    }
}
