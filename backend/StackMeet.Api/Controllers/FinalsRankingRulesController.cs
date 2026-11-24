using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/competitions/finals-ranking-rules")]
public sealed class FinalsRankingRulesController(StackMeetDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompetitionFinalsRankingRuleResponse>>> List(CancellationToken ct)
    {
        var query = database.Competitions.AsNoTracking();

        query = SessionAuthorization.ScopeCompetitions(query, HttpContext);

        var competitionIds = await query
            .OrderBy(item => item.CompetitionCode)
            .Select(item => item.Id)
            .ToListAsync(ct);

        var governance = new FinalsRankingGovernanceService(database);
        var rules = new List<CompetitionFinalsRankingRuleResponse>(competitionIds.Count);
        foreach (var competitionId in competitionIds)
        {
            var effective = await governance.TryGetEffectiveRuleAsync(competitionId, ct);
            if (effective is null) continue;

            rules.Add(new CompetitionFinalsRankingRuleResponse(
                effective.CompetitionId,
                effective.RuleVersion,
                effective.ExplicitSelection));
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(rules);
    }
}

public sealed record CompetitionFinalsRankingRuleResponse(
    int CompetitionId,
    string RuleVersion,
    bool ExplicitSelection);
