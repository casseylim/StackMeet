namespace StackMeet.Api.Activities;

/// <summary>
/// Activity-neutral registry for modules known to this application build.
/// Existing competitions have no persisted module identifier yet, so a missing
/// module code resolves to the Sport Stacking compatibility default.
/// </summary>
public sealed class ActivityModuleRegistry
{
    public const string CompatibilityDefaultCode = SportStackingActivityModule.ModuleCode;

    private readonly IReadOnlyDictionary<string, IActivityModule> _modules;

    public ActivityModuleRegistry(IEnumerable<IActivityModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var registered = new Dictionary<string, IActivityModule>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules)
        {
            ArgumentNullException.ThrowIfNull(module);
            var code = module.Code?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("Activity modules must declare a non-empty code.");
            }

            if (!registered.TryAdd(code, module))
            {
                throw new InvalidOperationException($"Duplicate activity module code '{code}'.");
            }
        }

        if (!registered.ContainsKey(CompatibilityDefaultCode))
        {
            throw new InvalidOperationException(
                $"Compatibility activity module '{CompatibilityDefaultCode}' is not registered.");
        }

        _modules = registered;
    }

    public IReadOnlyCollection<IActivityModule> Modules =>
        _modules.Values.OrderBy(module => module.Code, StringComparer.OrdinalIgnoreCase).ToArray();

    public IActivityModule Resolve(string? moduleCode)
    {
        var normalized = Normalize(moduleCode);
        if (_modules.TryGetValue(normalized, out var module))
        {
            return module;
        }

        throw new KeyNotFoundException($"Unknown activity module '{normalized}'.");
    }

    public bool TryResolve(string? moduleCode, out IActivityModule? module) =>
        _modules.TryGetValue(Normalize(moduleCode), out module);

    private static string Normalize(string? moduleCode) =>
        string.IsNullOrWhiteSpace(moduleCode)
            ? CompatibilityDefaultCode
            : moduleCode.Trim();
}
