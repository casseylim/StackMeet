namespace StackMeet.Api.Dtos;

public sealed record ResultUpsertRequest(string Stage, string Type, string Participant, string Event, decimal[] Attempts, decimal Penalty, long? ExpectedRevision);
public sealed record ResultBatchRequest(ResultUpsertRequest[] Upserts, ResultDeleteRequest[] Deletes);
public sealed record ResultDeleteRequest(string Stage, string Type, string Participant, string Event, long? ExpectedRevision);
public sealed record CompetitionResultResponse(Guid Id, string Stage, string Type, string Participant, string Event, decimal[] Attempts, decimal Penalty, long Revision, DateTime UpdatedAt);
public sealed record CompetitionResultsResponse(long Revision, IReadOnlyList<CompetitionResultResponse> Results);
