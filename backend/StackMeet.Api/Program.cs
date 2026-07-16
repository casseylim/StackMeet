using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var apiKeyHeaderName = builder.Configuration["Security:ApiKeyHeaderName"] ?? "X-StackMeet-Api-Key";
var adminKeyHeaderName = builder.Configuration["Security:AdminKeyHeaderName"] ?? "X-StackMeet-Admin-Key";
var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? [];

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("StackMeetApiCors", policy =>
    {
        if (allowedOrigins.Length == 0) policy.AllowAnyOrigin(); else policy.WithOrigins(allowedOrigins);
        policy.WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type", apiKeyHeaderName, adminKeyHeaderName, "X-StackMeet-Updated-By");
    });
});
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddSingleton<PasswordHashService>();
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

        context.Response.StatusCode = string.IsNullOrWhiteSpace(configuredAdminKey)
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = string.IsNullOrWhiteSpace(configuredAdminKey) ? "Admin security is not configured." : "Valid admin authorization required." });
        return;
    }

    if (!RequiresApiAuth(path))
    {
        await next();
        return;
    }

    var configuredApiKey = app.Configuration["Security:ApiKey"];
    var suppliedApiKey = context.Request.Headers[apiKeyHeaderName].FirstOrDefault();
    if ((!string.IsNullOrWhiteSpace(configuredApiKey) && TimeConstantEquals(suppliedApiKey, configuredApiKey)) || IsLocalTestApiKey(context, suppliedApiKey))
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
        if (!await SessionCanAccessPath(session, path, database, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Session is not valid for this competition." });
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

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

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

static bool RequiresApiAuth(PathString path)
{
    return path.StartsWithSegments("/api")
        && !path.StartsWithSegments("/api/health")
        && !path.StartsWithSegments("/api/version")
        && !path.StartsWithSegments("/api/auth/login");
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

static async Task<bool> SessionCanAccessPath(SessionToken session, PathString path, StackMeetDbContext database, CancellationToken ct)
{
    if (path.StartsWithSegments("/api/state", out var stateRemaining))
    {
        var requestedCompetition = stateRemaining.Value?.Trim('/').Split('/')[0];
        return string.Equals(requestedCompetition, session.CompetitionId, StringComparison.OrdinalIgnoreCase);
    }

    if (path.StartsWithSegments("/api/competitions", out var competitionRemaining))
    {
        var firstSegment = competitionRemaining.Value?.Trim('/').Split('/')[0];
        if (int.TryParse(firstSegment, out var competitionId))
        {
            return await database.Competitions.AsNoTracking().AnyAsync(item => item.Id == competitionId && item.CompetitionKey == session.CompetitionId, ct);
        }
    }

    return true;
}

static bool IsLocalTestApiKey(HttpContext context, string? suppliedApiKey)
{
    var host = context.Request.Host.Host;
    return (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1")
        && string.Equals(suppliedApiKey, "Vsep@3692", StringComparison.Ordinal);
}
