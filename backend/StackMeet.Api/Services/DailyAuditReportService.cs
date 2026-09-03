using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;

namespace StackMeet.Api.Services;

/// <summary>Generates and emails the previous day's user activity report at midnight MYT.</summary>
/// <remarks>The worker uses MSSQL audit rows and remains independent of competition JSON state.</remarks>
public sealed class DailyAuditReportService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<DailyAuditReportService> logger) : BackgroundService
{
    static readonly TimeZoneInfo MalaysiaTime = ResolveMalaysiaTimeZone();

    /// <summary>Waits for the next Malaysia midnight and sends one report for the completed local day.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MalaysiaTime);
            var nextMidnight = now.Date.AddDays(1);
            var delay = nextMidnight - now;
            await Task.Delay(delay, stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;

            try { await SendReport(nextMidnight.AddDays(-1), stoppingToken); }
            catch (Exception error) { logger.LogError(error, "Daily NADITrack audit report failed."); }
        }
    }

    /// <summary>Reads the completed local day, formats CSV, and sends it as an email attachment.</summary>
    /// <remarks>Only audit fields are included; password, API-key and secret values are never added.</remarks>
    async Task SendReport(DateTime reportDate, CancellationToken ct)
    {
        var recipient = configuration["AuditReport:Recipient"];
        if (string.IsNullOrWhiteSpace(recipient))
        {
            logger.LogWarning("Daily NADITrack audit report skipped because AuditReport:Recipient is not configured.");
            return;
        }

        var day = DateOnly.FromDateTime(reportDate);
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<StackMeetDbContext>();
        var emails = scope.ServiceProvider.GetRequiredService<AccountEmailService>();
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(reportDate.Date, MalaysiaTime);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(reportDate.Date.AddDays(1), MalaysiaTime);
        var rows = await database.AuditLogs.AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Competition)
            .Where(item => item.CreatedAt >= startUtc && item.CreatedAt < endUtc)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(ct);

        var csv = BuildCsv(rows);
        await emails.SendAuditReportEmail(recipient, day, csv, ct);
        logger.LogInformation("Sent daily NADITrack audit report for {ReportDate} with {Count} rows.", day, rows.Count);
    }

    /// <summary>Builds an RFC4180-compatible UTF-8 CSV with MYT display timestamps.</summary>
    static byte[] BuildCsv(IEnumerable<Models.AuditLog> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Timestamp MYT,Action,Entity Type,Entity ID,User Email,Competition,IP Address,Old Value,New Value");
        foreach (var row in rows)
        {
            var timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc), MalaysiaTime)
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            builder.AppendLine(string.Join(',', new[]
            {
                timestamp, row.Action, row.EntityType, row.EntityId ?? "", row.User?.Email ?? "",
                row.Competition?.CompetitionKey ?? "", row.IpAddress ?? "", row.OldValueJson ?? "", row.NewValueJson ?? ""
            }.Select(Csv))); 
        }
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>Escapes one CSV field, including commas, quotes and line breaks.</summary>
    static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    static TimeZoneInfo ResolveMalaysiaTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
    }
}
