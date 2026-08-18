using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;

namespace StackMeet.Api.Services;

public sealed class ParticipantCertificateProjectionService(StackMeetDbContext database)
{
    public async Task<ParticipantCertificateProjection?> Resolve(int competitionId, string participantCode, CancellationToken ct)
    {
        var stacker = await database.Stackers.AsNoTracking().SingleOrDefaultAsync(item => item.CompetitionId == competitionId && item.StackerCode == participantCode, ct);
        if (stacker is null) return null;
        var name = string.Join(" ", new[] { stacker.FirstName, stacker.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        return new ParticipantCertificateProjection(
            stacker.StackerCode,
            string.IsNullOrWhiteSpace(name) ? stacker.StackerCode : name,
            stacker.Club ?? "Independent",
            string.IsNullOrWhiteSpace(stacker.CustomDivision) ? "Open / Unassigned" : stacker.CustomDivision.Trim(),
            stacker.Gender,
            stacker.Country ?? "",
            stacker.Region ?? "",
            stacker.IsSpecialStacker);
    }
}
