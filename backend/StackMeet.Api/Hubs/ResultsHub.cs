using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace StackMeet.Api.Hubs;

public sealed partial class ResultsHub : Hub
{
    public async Task JoinCompetition(string competitionId)
    {
        var normalized = competitionId.Trim().ToUpperInvariant();
        if (!CompetitionIdPattern().IsMatch(normalized))
        {
            throw new HubException("Invalid competition ID.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(normalized));
    }

    public static string GroupName(string competitionId) =>
        $"results:{competitionId.Trim().ToUpperInvariant()}";

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{2,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CompetitionIdPattern();
}
