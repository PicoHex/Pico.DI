namespace Pico.IoC;

public sealed class SvcContainerFactory(ISvcProviderFactory providerFactory)
{
    public ISvcContainer CreateContainer() => new SvcContainer(providerFactory);
}
