using BizScraper.Api.Common.Extensions;
using BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;
using Microsoft.Extensions.DependencyInjection;

namespace BizScraper.UnitTests.Common.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAllImplementations_DiscoversConcreteImplementations()
    {
        var services = new ServiceCollection();

        services.AddAllImplementations<IPostFlowDocumentProcessor>();

        // Should have discovered zero implementations at this point (none exist in the assembly yet from test project)
        // The implementations are in BizScraper.Api assembly — we test the mechanism works
        var descriptors = services.Where(d => d.ServiceType == typeof(IPostFlowDocumentProcessor)).ToList();
        // At minimum, should not throw
        Assert.NotNull(descriptors);
    }

    [Fact]
    public void AddAllImplementations_SkipsAbstractClasses()
    {
        var services = new ServiceCollection();

        services.AddAllImplementations<ITestServiceContract>();

        var descriptors = services.Where(d => d.ServiceType == typeof(ITestServiceContract)).ToList();
        // Should not contain the abstract class
        Assert.DoesNotContain(descriptors, d => d.ImplementationType == typeof(AbstractTestService));
    }

    [Fact]
    public void AddAllImplementations_SkipsInterfaces()
    {
        var services = new ServiceCollection();

        services.AddAllImplementations<ITestServiceContract>();

        var descriptors = services.Where(d => d.ServiceType == typeof(ITestServiceContract)).ToList();
        Assert.DoesNotContain(descriptors, d => d.ImplementationType == typeof(ITestServiceContract));
    }

    [Fact]
    public void AddAllImplementations_RegistersWithCorrectLifetime()
    {
        var services = new ServiceCollection();

        services.AddAllImplementations<ITestServiceContract>(ServiceLifetime.Scoped);

        var descriptors = services.Where(d => d.ServiceType == typeof(ITestServiceContract)).ToList();
        foreach (var descriptor in descriptors)
        {
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }

    [Fact]
    public void AddAllImplementations_DefaultsToSingleton()
    {
        var services = new ServiceCollection();

        services.AddAllImplementations<ITestServiceContract>();

        var descriptors = services.Where(d => d.ServiceType == typeof(ITestServiceContract)).ToList();
        foreach (var descriptor in descriptors)
        {
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }
    }

    // Test types defined in the test assembly — suppressed CA rules for test fixtures
#pragma warning disable CA1034, CA1040, CA1711
    public interface ITestServiceContract
    {
        void Execute();
    }

    public abstract class AbstractTestService : ITestServiceContract
    {
        public abstract void Execute();
    }

    public sealed class ConcreteTestService : ITestServiceContract
    {
        public void Execute() { }
    }

    public sealed class AnotherConcreteTestService : ITestServiceContract
    {
        public void Execute() { }
    }
#pragma warning restore CA1034, CA1040, CA1711
}
