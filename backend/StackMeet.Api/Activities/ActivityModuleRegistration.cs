using Microsoft.Extensions.DependencyInjection;
using StackMeet.Api.Activities.SportStacking.Identity;

namespace StackMeet.Api.Activities;

/// <summary>
/// Dependency-injection registration seam for the activity module system.
/// The registry, compatibility resolver and assignment policy are infrastructure
/// only; activity-specific domain rules remain inside their own modules.
/// </summary>
public static class ActivityModuleRegistration
{
    public static IServiceCollection AddNadiTrackActivityModules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IActivityModule, SportStackingActivityModule>();
        services.AddSingleton<IActivityModule, ChessActivityModule>();
        services.AddSingleton<ActivityModuleRegistry>();
        services.AddSingleton<CompetitionActivityResolver>();
        services.AddSingleton<ActivityAssignmentPolicy>();
        services.AddScoped<SportStackerCareerProfileService>();
        return services;
    }
}
