namespace StackMeet.Api.Dtos;

public sealed record CompetitionAdminSummaryResponse(
    int Id,
    string CompetitionCode,
    string CompetitionKey,
    string CompetitionName,
    string Venue,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    bool IsPubliclyListed,
    bool HasPassword,
    bool HasState,
    DateTime? StateCreatedAt,
    DateTime? StateUpdatedAt,
    DateTime? ArchivedAt,
    string? ArchivedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CompetitionAdminUpsertRequest(
    string? CompetitionCode,
    string? CompetitionKey,
    string CompetitionName,
    string Venue,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    string? Password,
    bool IsPubliclyListed = false);

public sealed record CompetitionAdminPasswordRequest(string Password);

public sealed record CompetitionAdminStatusRequest(string Status);

public sealed record CompetitionAdminArchiveRequest(string? ArchivedBy);

public sealed record CompetitionAdminResetStateRequest(string Confirmation, bool ResultsOnly);

public sealed record CompetitionAdminDeleteRequest(string Confirmation);

public sealed record CompetitionJsonExportResponse(string CompetitionKey, DateTime ExportedAtUtc, string JsonData);
