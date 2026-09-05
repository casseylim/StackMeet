using Microsoft.Extensions.DependencyInjection;

namespace StackMeet.Api.Activities;

/// <summary>
/// Dependency-injection registration seam for the activity module system.
/// Phase 2 intentionally does not invoke this from Program.cs yet, so introducing
/// the seam cannot affect the current request pipeline or application behavior.
/// </summary>
public static class ActivityModuleRegistration
{
    public static IServiceCollection AddNadiTrackActivityModules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IActivityModule, SportStackingActivityModule>();
        services.AddSingleton<ActivityModuleRegistry>();
        return services;
    }
}
