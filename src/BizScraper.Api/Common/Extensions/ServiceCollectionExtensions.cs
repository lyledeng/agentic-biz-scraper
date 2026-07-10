namespace BizScraper.Api.Common.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to support convention-based DI registration.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scans the assembly containing <typeparamref name="TInterface"/> for all concrete implementations
    /// and registers each with the specified lifetime.
    /// </summary>
    public static IServiceCollection AddAllImplementations<TInterface>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        var interfaceType = typeof(TInterface);
        var implementations = interfaceType.Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && interfaceType.IsAssignableFrom(t));

        foreach (var impl in implementations)
        {
            services.Add(new ServiceDescriptor(interfaceType, impl, lifetime));
        }

        return services;
    }
}
