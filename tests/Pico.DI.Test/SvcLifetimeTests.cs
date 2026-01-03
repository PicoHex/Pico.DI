namespace Pico.DI.Test;

/// <summary>
/// Tests for SvcLifetime enum.
/// </summary>
public class SvcLifetimeTests : XUnitTestBase
{
    [Fact]
    public void SvcLifetime_Transient_HasExpectedValue()
    {
        // Assert
        Assert.Equal(0, (int)SvcLifetime.Transient);
    }

    [Fact]
    public void SvcLifetime_Singleton_HasExpectedValue()
    {
        // Assert
        Assert.Equal(1, (int)SvcLifetime.Singleton);
    }

    [Fact]
    public void SvcLifetime_Scoped_HasExpectedValue()
    {
        // Assert
        Assert.Equal(2, (int)SvcLifetime.Scoped);
    }

    [Fact]
    public void SvcLifetime_AllValues_AreDefined()
    {
        // Act
        var values = Enum.GetValues<SvcLifetime>();

        // Assert
        Assert.Equal(3, values.Length);
        Assert.Contains(SvcLifetime.Transient, values);
        Assert.Contains(SvcLifetime.Singleton, values);
        Assert.Contains(SvcLifetime.Scoped, values);
    }

    [Fact]
    public void SvcLifetime_ToString_ReturnsExpectedNames()
    {
        // Assert
        Assert.Equal("Transient", SvcLifetime.Transient.ToString());
        Assert.Equal("Singleton", SvcLifetime.Singleton.ToString());
        Assert.Equal("Scoped", SvcLifetime.Scoped.ToString());
    }
}
