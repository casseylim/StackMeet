using StackMeet.Api.Models;

namespace StackMeet.Api.Activities;

/// <summary>
/// Resolves the activity module selected by the shared Competition model.
/// Existing competitions keep a null selector and therefore continue through
/// the registry's Sport Stacking compatibility default without a data backfill.
/// </summary>
public sealed class CompetitionActivityResolver
{
    private readonly ActivityModuleRegistry _registry;

    public CompetitionActivityResolver(ActivityModuleRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IActivityModule Resolve(Competition competition)
    {
        ArgumentNullException.ThrowIfNull(competition);
        return _registry.Resolve(competition.ActivityModuleCode);
    }
}
