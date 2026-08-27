using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;
using StackMeet.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);
var apiKeyHeaderName = builder.Configuration["Security:ApiKeyHeaderName"] ?? "X-StackMeet-Api-Key";
var adminKeyHeaderName = builder.Configuration["Security:AdminKeyHeaderName"] ?? "X-StackMeet-Admin-Key";
var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? [];

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("StackMeetApiCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin();
        }

        policy.WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type", apiKeyHeaderName, adminKeyHeaderName, "X-StackMeet-Updated-By", "If-Match")
            .WithExposedHeaders("ETag");
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many login attempts. Please wait before trying again." },
            cancellationToken);
    };
    options.AddPolicy("Login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddSingleton<PasswordHashService>();
builder.Services.AddScoped<CompetitionPermissionService>();
builder.Services.AddScoped<CompetitionParticipantReferenceService>();
builder.Services.AddScoped<AccountTokenService>();
builder.Services.AddHttpClient<AccountEmailService>(client =>
{
    // Bound external email calls so a provider outage cannot hold an API request indefinitely.
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<AccountLinkService>();
builder.Services.AddScoped<ProtectedSettingService>();
builder.Services.AddSingleton<CompetitionAssetStorage>();
builder.Services.AddScoped<AuditLogService>();
// Daily audit-email generation is disabled to reduce background server workload; on-demand audit export remains available.
builder.Services.AddDbContext<StackMeetDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("StackMeet")));
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment()) app.UseHsts();

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseCors("StackMeetApiCors");
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api/admin"))
    {
        var configuredAdminKey = app.Configuration["Security:AdminKey"];
        var suppliedAdminKey = context.Request.Headers[adminKeyHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(configuredAdminKey) && TimeConstantEquals(suppliedAdminKey, configuredAdminKey))
        {
            await next();
            return;
        }

        var adminTokenService = context.RequestServices.GetRequiredService<SessionTokenService>();
        var adminBearerToken = BearerToken(context.Request.Headers.Authorization.FirstOrDefault());
        if (adminTokenService.TryValidate(adminBearerToken, out var adminSession)
            && adminSession.IsAccountSession)
        {
            var database = context.RequestServices.GetRequiredService<StackMeetDbContext>();
            if (!await AccountSessionIsCurrent(adminSession, database, context.RequestAborted)
                || !adminSession.IsSystemAdmin)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Login session is no longer valid. Sign in again." });
                return;
            }

            context.Items["StackMeetSession"] = adminSession;
            await next();
            return;
        }

        context.Response.StatusCode = string.IsNullOrWhiteSpace(configuredAdminKey)
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = string.IsNullOrWhiteSpace(configuredAdminKey) ? "Admin security is not configured." : "Valid admin key or system admin login required." });
        return;
    }

    if (!RequiresApiAuth(path))
    {
        await next();
        return;
    }

    var configuredApiKey = app.Configuration["Security:ApiKey"];
    var suppliedApiKey = context.Request.Headers[apiKeyHeaderName].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(configuredApiKey) && TimeConstantEquals(suppliedApiKey, configuredApiKey))
    {
        context.Items["StackMeetMaintenanceApiKey"] = true;
        await next();
        return;
    }

    var tokenService = context.RequestServices.GetRequiredService<SessionTokenService>();
    var bearerToken = BearerToken(context.Request.Headers.Authorization.FirstOrDefault());
    if (tokenService.TryValidate(bearerToken, out var session))
    {
        var database = context.RequestServices.GetRequiredService<StackMeetDbContext>();

        if (session.IsAccountSession
            && !await AccountSessionIsCurrent(session, database, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Login session is no longer valid. Sign in again." });
            return;
        }

        if (!await SessionCanAccessPath(session, path, database, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Session is not valid for this competition." });
            return;
        }

        var statusRestriction = await CompetitionStatusRestriction(
            session,
            path,
            context.Request.Method,
            database,
            context.RequestAborted);
        if (statusRestriction is { } restriction)
        {
            context.Response.StatusCode = restriction.StatusCode;
            await context.Response.WriteAsJsonAsync(new { error = restriction.Error });
            return;
        }

        context.Items["StackMeetSession"] = session;
        await next();
        return;
    }

    if (string.IsNullOrWhiteSpace(configuredApiKey) && string.IsNullOrWhiteSpace(tokenService.SigningKey))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "API security is not configured." });
        return;
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsJsonAsync(new { error = "Valid API key or login session required." });
});

app.MapControllers();
app.MapHub<ResultsHub>("/hubs/results");
app.MapGet("/{competitionId}/Results", ResultsPortal);
app.MapGet("/{competitionId}/Results/{**section}", ResultsPortal);
if (app.Environment.IsDevelopment())
{
    app.MapGet("/debug", (IWebHostEnvironment env) => Results.Json(new
    {
        env.ContentRootPath,
        env.WebRootPath,
        WebRootExists = Directory.Exists(env.WebRootPath),
        IndexExists = File.Exists(Path.Combine(env.WebRootPath, "index.html")),
        CurrentDirectory = Directory.GetCurrentDirectory()
    }));
}

app.Run();

static Task ResultsPortal(HttpContext context, IWebHostEnvironment environment)
{
    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache, no-store";
    return context.Response.SendFileAsync(Path.Combine(environment.WebRootPath, "results", "index.html"));
}

static bool RequiresApiAuth(PathString path)
{
    return path.StartsWithSegments("/api")
        && !path.StartsWithSegments("/api/health")
        && !path.StartsWithSegments("/api/version")
        && !path.StartsWithSegments("/api/auth/login")
        && !path.StartsWithSegments("/api/auth/forgot-password")
        && !path.StartsWithSegments("/api/auth/activate")
        && !path.StartsWithSegments("/api/auth/reset-password")
        && !path.StartsWithSegments("/api/public");
}

static bool TimeConstantEquals(string? supplied, string expected)
{
    if (string.IsNullOrEmpty(supplied) || supplied.Length != expected.Length) return false;
    var difference = 0;
    for (var index = 0; index < expected.Length; index++) difference |= supplied[index] ^ expected[index];
    return difference == 0;
}

static string? BearerToken(string? authorization)
{
    const string prefix = "Bearer ";
    return authorization?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
        ? authorization[prefix.Length..].Trim()
        : null;
}

static async Task<(int StatusCode, string Error)?> CompetitionStatusRestriction(
    SessionToken session,
    PathString path,
    string method,
    StackMeetDbContext database,
    CancellationToken ct)
{
    var competitionKey = await RequestedCompetitionKey(session, path, database, ct);
    if (competitionKey is null)
    {
        return null;
    }

    var lifecycle = await database.Competitions
        .AsNoTracking()
        .Where(item => item.CompetitionKey == competitionKey)
        .Select(item => new { item.Status, item.ArchivedAt })
        .SingleOrDefaultAsync(ct);

    if (lifecycle is null)
    {
        return (StatusCodes.Status403Forbidden, "Competition is no longer available.");
    }

    if (lifecycle.ArchivedAt is not null
        || string.Equals(lifecycle.Status, "Archived", StringComparison.OrdinalIgnoreCase))
    {
        return (StatusCodes.Status403Forbidden, "Competition is archived.");
    }

    if (IsWriteMethod(method)
        && string.Equals(lifecycle.Status, "Closed", StringComparison.OrdinalIgnoreCase))
    {
        return (StatusCodes.Status409Conflict, "Competition is closed and read-only.");
    }

    return null;
}

static bool IsWriteMethod(string method) =>
    !HttpMethods.IsGet(method)
    && !HttpMethods.IsHead(method)
    && !HttpMethods.IsOptions(method);

static async Task<bool> AccountSessionIsCurrent(
    SessionToken session,
    StackMeetDbContext database,
    CancellationToken ct)
{
    if (!session.IsAccountSession
        || session.UserId is null
        || session.SessionVersion is null)
    {
        return false;
    }

    return await database.AppUsers
        .AsNoTracking()
        .AnyAsync(item =>
            item.Id == session.UserId.Value
            && item.IsActive
            && item.SessionVersion == session.SessionVersion.Value
            && item.IsSystemAdmin == session.IsSystemAdmin,
            ct);
}

static async Task<bool> SessionCanAccessPath(SessionToken session, PathString path, StackMeetDbContext database, CancellationToken ct)
{
    if (path.StartsWithSegments("/api/state", out var stateRemaining))
    {
        var requestedCompetition = stateRemaining.Value?.Trim('/').Split('/')[0];
        if (string.IsNullOrWhiteSpace(requestedCompetition)) return false;
        if (!session.IsAccountSession)
        {
            return string.Equals(requestedCompetition, session.CompetitionId, StringComparison.OrdinalIgnoreCase);
        }

        return session.IsSystemAdmin || await database.CompetitionUsers.AsNoTracking().AnyAsync(item =>
            item.IsActive
            && item.UserId == session.UserId
            && item.User.IsActive
            && item.Competition.CompetitionKey == requestedCompetition,
            ct);
    }

    if (path.StartsWithSegments("/api/competitions", out var competitionRemaining))
    {
        var firstSegment = competitionRemaining.Value?.Trim('/').Split('/')[0];
        if (int.TryParse(firstSegment, out var competitionId))
        {
            if (!session.IsAccountSession)
            {
                return await database.Competitions.AsNoTracking().AnyAsync(item => item.Id == competitionId && item.CompetitionKey == session.CompetitionId, ct);
            }

            return session.IsSystemAdmin || await database.CompetitionUsers.AsNoTracking().AnyAsync(item =>
                item.IsActive
                && item.UserId == session.UserId
                && item.User.IsActive
                && item.CompetitionId == competitionId,
                ct);
        }
    }

    return true;
}

// Resolves the competition key affected by the current request so account sessions can reuse
// the existing competition lifecycle restrictions during the auth migration.
static async Task<string?> RequestedCompetitionKey(SessionToken session, PathString path, StackMeetDbContext database, CancellationToken ct)
{
    if (!session.IsAccountSession && !string.IsNullOrWhiteSpace(session.CompetitionId))
    {
        return session.CompetitionId;
    }

    if (path.StartsWithSegments("/api/state", out var stateRemaining))
    {
        return stateRemaining.Value?.Trim('/').Split('/')[0];
    }

    if (path.StartsWithSegments("/api/competitions", out var competitionRemaining))
    {
        var firstSegment = competitionRemaining.Value?.Trim('/').Split('/')[0];
        if (int.TryParse(firstSegment, out var competitionId))
        {
            return await database.Competitions
                .AsNoTracking()
                .Where(item => item.Id == competitionId)
                .Select(item => item.CompetitionKey)
                .SingleOrDefaultAsync(ct);
        }
    }

    return null;
}
