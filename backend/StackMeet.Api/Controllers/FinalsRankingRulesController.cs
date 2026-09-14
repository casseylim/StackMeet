using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking;
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
        var query = database.Competitions
            .AsNoTracking()
            .Where(item => string.IsNullOrWhiteSpace(item.ActivityModuleCode)
                || item.ActivityModuleCode == SportStackingActivityModule.ModuleCode);

        if (HttpContext.Items["StackMeetSession"] is SessionToken session)
        {
            if (session.IsAccountSession && !session.IsSystemAdmin)
            {
                query = query.Where(item => item.CompetitionUsers.Any(access =>
                    access.IsActive
                    && access.UserId == session.UserId
                    && access.User.IsActive));
            }
            else if (!session.IsAccountSession)
            {
                query = query.Where(item => item.CompetitionKey == session.CompetitionId);
            }
        }

        var competitionIds = await query
            .OrderBy(item => item.CompetitionCode)
            .Select(item => item.Id)
            .ToListAsync(ct);

        var governance = new FinalsRankingGovernanceService(database);
        var rules = new List<CompetitionFinalsRankingRuleResponse>(competitionIds.Count);
        foreach (var competitionId in competitionIds)
        {
            var record = await governance.GetAsync(competitionId, ct);
            rules.Add(new CompetitionFinalsRankingRuleResponse(
                competitionId,
                FinalsRankingRuleVersions.ResolveStored(record?.RuleVersion),
                record is not null));
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(rules);
    }
}

public sealed record CompetitionFinalsRankingRuleResponse(
    int CompetitionId,
    string RuleVersion,
    bool ExplicitSelection);
