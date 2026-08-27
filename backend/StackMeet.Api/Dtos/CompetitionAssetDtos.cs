namespace StackMeet.Api.Dtos;
public sealed record CompetitionAssetResponse(string AssetType, string FileName, string ContentType, long FileSize, string PublicUrl, DateTime UpdatedAt);
