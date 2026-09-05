using Microsoft.Extensions.DependencyInjection;

namespace StackMeet.Api.Activities;

/// <summary>
/// Dependency-injection registration seam for the activity module system.
/// The registry and compatibility resolver are infrastructure only; existing
/// controllers continue to use their current Sport Stacking behavior until a
/// later bounded adapter phase explicitly consumes the resolver.
/// </summary>
public static class ActivityModuleRegistration
{
    public static IServiceCollection AddNadiTrackActivityModules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IActivityModule, SportStackingActivityModule>();
        services.AddSingleton<ActivityModuleRegistry>();
        services.AddSingleton<CompetitionActivityResolver>();
        return services;
    }
}
