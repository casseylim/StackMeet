using System.Text.Json;
using System.Text.Json.Nodes;

namespace StackMeet.Api.Services;

public static class CompetitionStateResetService
{
    static readonly string[] ResultKeys = ["results", "finalQualificationSnapshots", "notifications"];

    public static string ResetResultsOnly(string jsonData)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(jsonData)?.AsObject() ?? [];
        }
        catch (JsonException)
        {
            root = [];
        }

        foreach (var key in ResultKeys)
        {
            root[key] = new JsonArray();
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}