using Microsoft.Extensions.DependencyInjection;
using StackMeet.Api.Activities;
using StackMeet.Api.Models;

var services = new ServiceCollection();
services.AddNadiTrackActivityModules();
using var provider = services.BuildServiceProvider();

var registry = provider.GetRequiredService<ActivityModuleRegistry>();
var codes = registry.Modules
    .Select(module => module.Code)
    .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
    .ToArray();

Expect(codes.SequenceEqual(new[] { ChessActivityModule.ModuleCode, SportStackingActivityModule.ModuleCode }),
    "Phase 5A production DI registers exactly Chess and Sport Stacking");
Expect(registry.Resolve(null).Code == SportStackingActivityModule.ModuleCode,
    "Phase 5A null selector preserves Sport Stacking compatibility default");
Expect(registry.Resolve("   ").Code == SportStackingActivityModule.ModuleCode,
    "Phase 5A blank selector preserves Sport Stacking compatibility default");

var chess = registry.Resolve("CHESS");
Expect(chess.Code == ChessActivityModule.ModuleCode, "Phase 5A Chess resolves case-insensitively");
Expect(chess.DisplayName == "Chess", "Phase 5A Chess display name is stable");
Expect(chess.Version == "1", "Phase 5A Chess descriptor version is stable");
Expect(!chess.Capabilities.SupportsTeamEntries, "Phase 5A Chess does not advertise team entries");
Expect(!chess.Capabilities.SupportsCategories, "Phase 5A Chess does not advertise categories yet");
Expect(!chess.Capabilities.SupportsStages, "Phase 5A Chess does not advertise stages yet");
Expect(!chess.Capabilities.SupportsLiveResults, "Phase 5A Chess does not advertise live results yet");
Expect(!chess.Capabilities.SupportsCertificates, "Phase 5A Chess does not advertise certificates yet");
Expect(!chess.Capabilities.SupportsOfflinePackage, "Phase 5A Chess does not advertise offline packages yet");

var resolver = provider.GetRequiredService<CompetitionActivityResolver>();
Expect(resolver.Resolve(new Competition()).Code == SportStackingActivityModule.ModuleCode,
    "Phase 5A existing competition without selector still resolves Sport Stacking");
Expect(resolver.Resolve(new Competition { ActivityModuleCode = ChessActivityModule.ModuleCode }).Code == ChessActivityModule.ModuleCode,
    "Phase 5A persisted Chess selector resolves Chess");

var policy = provider.GetRequiredService<ActivityAssignmentPolicy>();
var emptySwitch = policy.Evaluate(new Competition(), ChessActivityModule.ModuleCode, hasDurableActivityData: false);
Expect(emptySwitch.IsAllowed && emptySwitch.ChangesEffectiveModule && emptySwitch.Module?.Code == ChessActivityModule.ModuleCode,
    "Phase 5A empty compatibility competition may switch to Chess");
var lockedSwitch = policy.Evaluate(new Competition(), ChessActivityModule.ModuleCode, hasDurableActivityData: true);
Expect(!lockedSwitch.IsAllowed && lockedSwitch.Failure == ActivityAssignmentFailure.DurableActivityData,
    "Phase 5A durable activity data still blocks Sport Stacking to Chess switch");

ExpectThrows<KeyNotFoundException>(() => registry.Resolve("unknown-phase5a-module"),
    "Phase 5A unknown production module still fails explicitly");

Console.WriteLine("Modular Platform Foundation v1 Phase 5A production module integration tests passed.");

static void Expect(bool condition, string scenario)
{
    if (!condition) throw new InvalidOperationException($"Failed scenario: {scenario}");
    Console.WriteLine($"PASS {scenario}");
}

static void ExpectThrows<TException>(Action action, string scenario) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        Console.WriteLine($"PASS {scenario}");
        return;
    }

    throw new InvalidOperationException($"Failed scenario: {scenario}");
}
