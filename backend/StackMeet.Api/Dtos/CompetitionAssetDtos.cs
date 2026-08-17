namespace StackMeet.Api.Dtos;
public sealed record CompetitionAssetResponse(string AssetType, string FileName, string ContentType, long FileSize, string Url, DateTime UpdatedAt);
