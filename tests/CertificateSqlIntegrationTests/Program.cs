using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using StackMeet.Api.Data;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_CertificateIntegrationTest_{DateTime.UtcNow:yyyyMMddHHmmssfff}";
var connection = $"Server={server};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
var root = Path.Combine(Path.GetTempPath(), $"stackmeet-certificates-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CertificateTemplatesPath"] = root }).Build();
var env = new TestEnvironment(root);
var storage = new CertificateTemplateStorage(config, env, NullLogger<CertificateTemplateStorage>.Instance);

try
{
    var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(connection).Options;
    await using (var db = new StackMeetDbContext(options))
    {
        await db.Database.MigrateAsync();
        var tables = await db.Database.SqlQueryRaw<string>("SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CertificateTemplate'").ToListAsync();
        Check(tables.Contains("CertificateTemplate"), "migration creates CertificateTemplate");
        var columns = await db.Database.SqlQueryRaw<string>("SELECT COLUMN_NAME AS [Value] FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CertificateTemplate'").ToListAsync();
        foreach (var column in new[] { "CompetitionId", "CertificateType", "TemplateVersion", "IsActive", "Sha256", "StoredFileName" }) Check(columns.Contains(column), $"migration column {column}");
        var competition = new Competition { CompetitionCode = "SQLTEST", CompetitionKey = "sql-test-key", CompetitionName = "SQL Certificate Test", Venue = "Kuala Lumpur", StartDate = new DateOnly(2026, 7, 11), EndDate = new DateOnly(2026, 7, 11), Status = "Draft", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Competitions.Add(competition); await db.SaveChangesAsync();
        await VersionAndActivation(db, competition.Id, storage);
        await ConstraintChecks(db, competition.Id);
    }
    await ConcurrentAllocation(options, databaseName);
    Console.WriteLine("Certificate SQL integration tests passed.");
}
finally
{
    try
    {
        await using var cleanup = new StackMeetDbContext(new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(connection).Options);
        await cleanup.Database.EnsureDeletedAsync();
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

static async Task VersionAndActivation(StackMeetDbContext db, int competitionId, CertificateTemplateStorage storage)
{
    var bytes1 = new byte[] { 1, 2, 3 }; var bytes2 = new byte[] { 4, 5, 6 };
    var file1 = await Save(storage, competitionId, bytes1); var file2 = await Save(storage, competitionId, bytes2);
    var now = DateTime.UtcNow;
    var v1 = new CertificateTemplate { CompetitionId = competitionId, CertificateType = CertificateTemplateDocumentService.Participation, Name = "v1", OriginalFileName = "v1.docx", StoredFileName = file1.name, ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", FileSize = bytes1.Length, Sha256 = file1.hash, TemplateVersion = 1, CreatedAt = now, UpdatedAt = now };
    var v2 = new CertificateTemplate { CompetitionId = competitionId, CertificateType = CertificateTemplateDocumentService.Participation, Name = "v2", OriginalFileName = "v2.docx", StoredFileName = file2.name, ContentType = v1.ContentType, FileSize = bytes2.Length, Sha256 = file2.hash, TemplateVersion = 2, CreatedAt = now, UpdatedAt = now };
    db.CertificateTemplates.AddRange(v1, v2); await db.SaveChangesAsync(); Check(v1.Sha256 == Hash(bytes1) && File.Exists(storage.FullPath(competitionId, v1.StoredFileName)), "upload metadata/hash/private storage");
    v1.IsActive = true; await db.SaveChangesAsync(); v1.IsActive = false; await db.SaveChangesAsync(); v2.IsActive = true; await db.SaveChangesAsync(); v2.IsActive = false; await db.SaveChangesAsync(); v1.IsActive = true; await db.SaveChangesAsync();
    Check(await db.CertificateTemplates.CountAsync(x => x.CompetitionId == competitionId && x.IsActive) == 1, "activation keeps one active template");
    db.AuditLogs.Add(new AuditLog { CompetitionId = competitionId, Action = "CertificateTemplateUploaded", EntityType = "CertificateTemplate", CreatedAt = now }); await db.SaveChangesAsync(); Check(await db.AuditLogs.AnyAsync(x => x.CompetitionId == competitionId), "audit row persists");
    var before = await db.CertificateTemplates.AsNoTracking().SingleAsync(x => x.Id == v1.Id); db.CertificateTemplates.Remove(v1); await db.SaveChangesAsync(); storage.Delete(competitionId, v1.StoredFileName); Check(!File.Exists(storage.FullPath(competitionId, v1.StoredFileName)) && before.Sha256 == Hash(bytes1), "rollback cleanup preserves immutable source hash");
}

static async Task ConstraintChecks(StackMeetDbContext db, int competitionId)
{
    var row = await db.CertificateTemplates.SingleAsync(x => x.CompetitionId == competitionId); row.IsActive = true; await db.SaveChangesAsync();
    var duplicate = new CertificateTemplate { CompetitionId = competitionId, CertificateType = row.CertificateType, Name = "duplicate", OriginalFileName = "d.docx", StoredFileName = "d.docx", ContentType = row.ContentType, FileSize = 1, Sha256 = "D", TemplateVersion = row.TemplateVersion, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    db.CertificateTemplates.Add(duplicate); await ThrowsDb(async () => await db.SaveChangesAsync(), "unique version constraint"); db.ChangeTracker.Clear();
    var secondActive = await db.CertificateTemplates.SingleAsync(x => x.Id == row.Id); var active2 = new CertificateTemplate { CompetitionId = competitionId, CertificateType = row.CertificateType, Name = "active2", OriginalFileName = "a.docx", StoredFileName = "a.docx", ContentType = row.ContentType, FileSize = 1, Sha256 = "A", TemplateVersion = row.TemplateVersion + 1, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }; db.CertificateTemplates.Add(active2); await ThrowsDb(async () => await db.SaveChangesAsync(), "filtered active constraint"); db.ChangeTracker.Clear();
    Check(await db.CertificateTemplates.CountAsync(x => x.CompetitionId == competitionId && x.IsActive) == 1, "active constraint remains intact");
}

static async Task ConcurrentAllocation(DbContextOptions<StackMeetDbContext> options, string databaseName)
{
    await using var a = new StackMeetDbContext(options); await using var b = new StackMeetDbContext(options); var id = await a.Competitions.Select(x => x.Id).FirstAsync();
    async Task<int> Allocate(StackMeetDbContext db)
    { await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); var next = (await db.CertificateTemplates.Where(x => x.CompetitionId == id).MaxAsync(x => (int?)x.TemplateVersion) ?? 0) + 1; db.CertificateTemplates.Add(new CertificateTemplate { CompetitionId = id, CertificateType = CertificateTemplateDocumentService.Participation, Name = $"concurrent-{next}", OriginalFileName = "c.docx", StoredFileName = $"{Guid.NewGuid():N}.docx", ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", FileSize = 1, Sha256 = $"C{next}", TemplateVersion = next, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); await db.SaveChangesAsync(); await tx.CommitAsync(); return next; }
    var results = await Task.WhenAll(Task.Run(() => Allocate(a)), Task.Run(() => Allocate(b)).ContinueWith(t => t.IsFaulted ? -1 : t.Result));
    Check(results.Distinct().Count() == results.Length || results.Contains(-1), "concurrent allocation is unique or controlled conflict");
    Check(await a.CertificateTemplates.Where(x => x.CompetitionId == id).GroupBy(x => x.TemplateVersion).AllAsync(g => g.Count() == 1), "concurrent versions remain unique");
}

static async Task<(string name, string hash)> Save(CertificateTemplateStorage storage, int id, byte[] bytes) { await using var input = new MemoryStream(bytes); return await storage.SaveAsync(id, input, CancellationToken.None); }
static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException($"FAIL {name}"); Console.WriteLine($"PASS {name}"); }
static async Task ThrowsDb(Func<Task> action, string name) { try { await action(); throw new InvalidOperationException($"FAIL {name}: no exception"); } catch (DbUpdateException) { Console.WriteLine($"PASS {name}"); } }

sealed class TestEnvironment(string root) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "CertificateSqlIntegrationTests";
    public string EnvironmentName { get; set; } = "Test";
    public string WebRootPath { get; set; } = root;
    public string ContentRootPath { get; set; } = root;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
