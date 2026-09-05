using StackMeet.Api.Models;

namespace StackMeet.Api.Activities;

/// <summary>
/// Resolves the activity module for a competition without requiring a persisted
/// activity column during the compatibility phase. Existing competitions are
/// intentionally mapped to the registry's Sport Stacking compatibility default.
/// A future schema-backed selector can replace the compatibility code source
/// without changing callers of this resolver.
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
        return _registry.Resolve(moduleCode: null);
    }
}
