using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/state")]
public sealed class CompetitionStateController(StackMeetDbContext database) : ControllerBase
{
    [HttpGet("{competitionKey}")]
    public async Task<IActionResult> Get(string competitionKey, CancellationToken cancellationToken)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        if (!CompetitionKeyRules.IsValid(normalizedKey))
        {
            return BadRequest(new { error = "Competition key must be 3-50 characters: A-Z, 0-9, underscore or hyphen." });
        }

        var jsonData = await ReadJsonData(normalizedKey, cancellationToken);
        return jsonData is null ? NotFound() : Content(jsonData, "application/json");
    }

    [HttpPost("{competitionKey}")]
    public async Task<IActionResult> Save(string competitionKey, CancellationToken cancellationToken)
    {
        var normalizedKey = CompetitionKeyRules.Normalize(competitionKey);
        if (!CompetitionKeyRules.IsValid(normalizedKey))
        {
            return BadRequest(new { error = "Competition key must be 3-50 characters: A-Z, 0-9, underscore or hyphen." });
        }

        using var reader = new StreamReader(Request.Body);
        var jsonData = await reader.ReadToEndAsync(cancellationToken);
        var validationError = ValidateStateJson(jsonData);
        if (validationError is not null)
        {
            return BadRequest(new { error = validationError });
        }

        var updatedBy = Request.Headers["X-StackMeet-Updated-By"].FirstOrDefault();

        var updated = await ExecuteStateCommand(
            "UPDATE [dbo].[CompetitionState] SET [JsonData] = @jsonData, [UpdatedAt] = SYSUTCDATETIME(), [UpdatedBy] = @updatedBy WHERE [CompetitionKey] = @competitionKey",
            normalizedKey,
            jsonData,
            updatedBy,
            cancellationToken);

        if (updated == 0)
        {
            await ExecuteStateCommand(
                "INSERT INTO [dbo].[CompetitionState] ([CompetitionKey], [JsonData], [SchemaVersion], [UpdatedAt], [UpdatedBy]) VALUES (@competitionKey, @jsonData, '0.9-online', SYSUTCDATETIME(), @updatedBy)",
                normalizedKey,
                jsonData,
                updatedBy,
                cancellationToken);
        }

        return NoContent();
    }

    static string? ValidateStateJson(string jsonData)
    {
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            return "Competition state must contain a JSON object.";
        }

        try
        {
            using var document = JsonDocument.Parse(jsonData);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? null
                : "Competition state root must be a JSON object.";
        }
        catch (JsonException)
        {
            return "Competition state contains malformed JSON.";
        }
    }

    async Task<string?> ReadJsonData(string competitionKey, CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TOP(1) [JsonData] FROM [dbo].[CompetitionState] WHERE [CompetitionKey] = @competitionKey";
            AddParameter(command, "@competitionKey", competitionKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : (string)result;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    async Task<int> ExecuteStateCommand(string sql, string competitionKey, string jsonData, string? updatedBy, CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "@competitionKey", competitionKey);
            AddParameter(command, "@jsonData", jsonData);
            AddParameter(command, "@updatedBy", string.IsNullOrWhiteSpace(updatedBy) ? DBNull.Value : updatedBy);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}