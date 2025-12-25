namespace Pico.DI.Test;

/// <summary>
/// Tests for IEnumerable&lt;T&gt; auto-injection functionality.
/// </summary>
public class SvcContainerEnumerableInjectionTests : SvcContainerTestBase
{
    #region Test Services

    public interface INotifier
    {
        string Notify(string message);
    }

    public class EmailNotifier : INotifier
    {
        public string Notify(string message) => $"Email: {message}";
    }

    public class SmsNotifier : INotifier
    {
        public string Notify(string message) => $"SMS: {message}";
    }

    public class PushNotifier : INotifier
    {
        public string Notify(string message) => $"Push: {message}";
    }

    public class NotificationService(IEnumerable<INotifier> notifiers)
    {
        public IEnumerable<INotifier> Notifiers { get; } = notifiers;

        public IEnumerable<string> NotifyAll(string message) =>
            Notifiers.Select(n => n.Notify(message));
    }

    #endregion

    [Fact]
    public void GetService_IEnumerable_ReturnsAllImplementations()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<INotifier>(scope => new EmailNotifier());
        container.RegisterTransient<INotifier>(scope => new SmsNotifier());
        container.RegisterTransient<INotifier>(scope => new PushNotifier());

        using var scope = container.CreateScope();

        // Act
        var notifiers = scope.GetService<IEnumerable<INotifier>>();

        // Assert
        Assert.NotNull(notifiers);
        var notifierList = notifiers.ToList();
        Assert.Equal(3, notifierList.Count);
        Assert.Contains(notifierList, n => n is EmailNotifier);
        Assert.Contains(notifierList, n => n is SmsNotifier);
        Assert.Contains(notifierList, n => n is PushNotifier);
    }

    [Fact]
    public void GetService_IEnumerable_ReturnsEmptyForUnregistered()
    {
        // Arrange
        using var container = new SvcContainer();
        using var scope = container.CreateScope();

        // Act
        var notifiers = scope.GetService<IEnumerable<INotifier>>();

        // Assert
        Assert.NotNull(notifiers);
        Assert.Empty(notifiers);
    }

    [Fact]
    public void GetService_IEnumerable_RespectsLifetimes()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterSingleton<INotifier>(scope => new EmailNotifier());
        container.RegisterTransient<INotifier>(scope => new SmsNotifier());

        using var scope = container.CreateScope();

        // Act
        var notifiers1 = scope.GetService<IEnumerable<INotifier>>().ToList();
        var notifiers2 = scope.GetService<IEnumerable<INotifier>>().ToList();

        // Assert
        // Singleton should be same instance
        Assert.Same(notifiers1[0], notifiers2[0]);
        // Transient should be different instance
        Assert.NotSame(notifiers1[1], notifiers2[1]);
    }

    [Fact]
    public void ConstructorInjection_IEnumerable_InjectsAllImplementations()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<INotifier>(scope => new EmailNotifier());
        container.RegisterTransient<INotifier>(scope => new SmsNotifier());
        container.RegisterTransient<NotificationService>(scope => new NotificationService(
            scope.GetService<IEnumerable<INotifier>>()
        ));

        using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<NotificationService>();
        var results = service.NotifyAll("Hello").ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains("Email: Hello", results);
        Assert.Contains("SMS: Hello", results);
    }

    [Fact]
    public void IServiceProvider_GetService_IEnumerable_Works()
    {
        // Arrange
        using var container = new SvcContainer();
        container.RegisterTransient<INotifier>(scope => new EmailNotifier());
        container.RegisterTransient<INotifier>(scope => new SmsNotifier());

        using var adapter = container.CreateServiceProviderScope();

        // Act
        var notifiers =
            ((IServiceProvider)adapter).GetService(typeof(IEnumerable<INotifier>))
            as IEnumerable<INotifier>;

        // Assert
        Assert.NotNull(notifiers);
        Assert.Equal(2, notifiers.Count());
    }

    [Fact]
    public void IServiceProvider_GetService_IEnumerable_ReturnsEmptyArrayForUnregistered()
    {
        // Arrange
        using var container = new SvcContainer();
        using var adapter = container.CreateServiceProviderScope();

        // Act
        var notifiers = ((IServiceProvider)adapter).GetService(typeof(IEnumerable<INotifier>));

        // Assert
        Assert.NotNull(notifiers);
        Assert.IsAssignableFrom<IEnumerable<INotifier>>(notifiers);
        Assert.Empty((IEnumerable<INotifier>)notifiers);
    }
}
