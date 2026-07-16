using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/state")]
public sealed class CompetitionStateController(StackMeetDbContext database) : ControllerBase
{
    [HttpGet("{competitionKey}")]
    public async Task<IActionResult> Get(string competitionKey, CancellationToken cancellationToken)
    {
        var jsonData = await ReadJsonData(competitionKey, cancellationToken);
        return jsonData is null ? NotFound() : Content(jsonData, "application/json");
    }

    [HttpPost("{competitionKey}")]
    public async Task<IActionResult> Save(string competitionKey, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var jsonData = await reader.ReadToEndAsync(cancellationToken);
        var updatedBy = Request.Headers["X-StackMeet-Updated-By"].FirstOrDefault();

        var updated = await ExecuteStateCommand(
            "UPDATE [dbo].[CompetitionState] SET [JsonData] = @jsonData, [UpdatedAt] = SYSUTCDATETIME(), [UpdatedBy] = @updatedBy WHERE [CompetitionKey] = @competitionKey",
            competitionKey,
            jsonData,
            updatedBy,
            cancellationToken);

        if (updated == 0)
        {
            await ExecuteStateCommand(
                "INSERT INTO [dbo].[CompetitionState] ([CompetitionKey], [JsonData], [SchemaVersion], [UpdatedAt], [UpdatedBy]) VALUES (@competitionKey, @jsonData, '0.9-online', SYSUTCDATETIME(), @updatedBy)",
                competitionKey,
                jsonData,
                updatedBy,
                cancellationToken);
        }

        return NoContent();
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