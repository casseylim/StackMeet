using StackMeet.Api.Models;

namespace StackMeet.Api.Activities;

public enum ActivityAssignmentFailure
{
    None,
    MissingModuleCode,
    UnknownModuleCode,
    DurableActivityData
}

public sealed record ActivityAssignmentDecision(
    IActivityModule? Module,
    bool ChangesEffectiveModule,
    ActivityAssignmentFailure Failure,
    string? Error)
{
    public bool IsAllowed => Failure == ActivityAssignmentFailure.None && Module is not null;
}

/// <summary>
/// Activity-neutral policy for assigning a registered module to a competition.
/// Effective module changes are allowed only before durable activity data exists.
/// </summary>
public sealed class ActivityAssignmentPolicy(ActivityModuleRegistry registry)
{
    public ActivityAssignmentDecision Evaluate(
        Competition competition,
        string? requestedModuleCode,
        bool hasDurableActivityData)
    {
        ArgumentNullException.ThrowIfNull(competition);

        if (string.IsNullOrWhiteSpace(requestedModuleCode))
        {
            return Reject(ActivityAssignmentFailure.MissingModuleCode, "Activity module code is required.");
        }

        if (!registry.TryResolve(requestedModuleCode, out var requestedModule) || requestedModule is null)
        {
            return Reject(ActivityAssignmentFailure.UnknownModuleCode, "Activity module code is not registered.");
        }

        var currentModule = registry.Resolve(competition.ActivityModuleCode);
        var changesEffectiveModule = !string.Equals(
            currentModule.Code,
            requestedModule.Code,
            StringComparison.OrdinalIgnoreCase);

        if (changesEffectiveModule && hasDurableActivityData)
        {
            return Reject(
                ActivityAssignmentFailure.DurableActivityData,
                "Activity module cannot be changed after durable activity data has been saved.");
        }

        return new ActivityAssignmentDecision(
            requestedModule,
            changesEffectiveModule,
            ActivityAssignmentFailure.None,
            Error: null);
    }

    private static ActivityAssignmentDecision Reject(ActivityAssignmentFailure failure, string error) =>
        new(Module: null, ChangesEffectiveModule: false, Failure: failure, Error: error);
}
