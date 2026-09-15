using Microsoft.Extensions.DependencyInjection;

namespace StackMeet.Api.Activities;

/// <summary>
/// Sport-Stacking-owned identity/profile service registrations.
/// Keeping activity-specific services here preserves the shared activity-module seam.
/// </summary>
public static class SportStackingIdentityRegistration
{
    public static IServiceCollection AddSportStackingIdentityProfileServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<SportStacking.Identity.PublicFinalsPlacementCareerIntegrationService>();
        return services;
    }
}
