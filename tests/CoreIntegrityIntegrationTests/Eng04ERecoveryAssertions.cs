using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

// A coordinated, quiescent recovery rehearsal confined to generated LocalDB fixtures.
internal static class Eng04ERecoveryAssertions
{
    public static async Task RunAsync(string connectionString, string key)
    {
        var source = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(source.DataSource, @"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase)
            || !source.IntegratedSecurity || !source.InitialCatalog.StartsWith("StackMeet_CoreIntegrityTest_", StringComparison.Ordinal)
            || !SafeName(source.InitialCatalog) || !string.IsNullOrEmpty(source.AttachDBFilename))
            throw new InvalidOperationException("ENG04E requires a generated CoreIntegrity LocalDB source.");
        var targetName = "StackMeet_CoreIntegrityTest_Recovery_" + Guid.NewGuid().ToString("N");
        Check(SafeName(targetName) && !string.Equals(source.InitialCatalog, targetName, StringComparison.OrdinalIgnoreCase), "distinct generated restore target");
        var target = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = targetName };
        var root = Path.Combine(Path.GetTempPath(), "StackMeet_ENG04E_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        var ct = timeout.Token;
        var masterBuilder = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = "master" };
        await using var master = new SqlConnection(masterBuilder.ConnectionString);
        var ownsRestoreTarget = false;
        try
        {
            await master.OpenAsync(ct);
            await using (var exists = master.CreateCommand())
            {
                exists.CommandText = "SELECT DB_ID(@name)";
                exists.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = targetName;
                Check(await exists.ExecuteScalarAsync(ct) is DBNull, "restore target does not exist");
                ownsRestoreTarget = true;
            }
            var watch = Stopwatch.StartNew();
            var now = DateTime.UtcNow;
            var day = DateOnly.FromDateTime(now);
            var secret = "synthetic-only-value";
            var settingKey = "ENG04E_" + Guid.NewGuid().ToString("N");
            var configurationValues = new Dictionary<string, string>
            {
                ["Security:SettingsEncryptionKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                ["Security:SessionSigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configurationValues
                .Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build();
            var assetBytes = Encoding.UTF8.GetBytes("synthetic competition asset");
            const string stateJson = """{"stackers":[],"results":[],"doubles":[],"relays":[],"finalQualificationSnapshots":[{"id":"Q1","status":"Approved","participantType":"Individual","event":"Cycle","selectedQualifiers":[{"participantId":"A1"}]}]}""";
            int competitionId;
            int stackerId;
            long identityId;
            string cipher;
            FinalsRankingGovernanceRecord snapshot;
            string[] logicalFiles;
            int[] counts;
            string sessionToken;
            await using (var db = Context(source.ConnectionString))
            {
                var competition = new Competition
                {
                    CompetitionCode = key, CompetitionKey = key, CompetitionName = "ENG04E synthetic recovery",
                    Venue = "LocalDB", StartDate = day, EndDate = day, Status = "Active",
                    CreatedAt = now, UpdatedAt = now, ResultsRevision = 2, IsPubliclyListed = true
                };
                db.Competitions.Add(competition);
                await db.SaveChangesAsync(ct);
                competitionId = competition.Id;
                var stacker = new Stacker
                {
                    CompetitionId = competitionId, StackerCode = "A1", FirstName = "Synthetic",
                    LastName = "Recovery", Gender = "F", Country = "MY", Paid = "No", CheckedIn = "No",
                    CreatedAt = now, UpdatedAt = now
                };
                var identity = new SportStackerIdentity
                {
                    NadiTrackId = "NDT-8K4M2PX", FirstName = "Synthetic", LastName = "Recovery",
                    Gender = "F", Country = "MY", IsPublicProfile = false, CreatedAt = now, UpdatedAt = now
                };
                db.AddRange(stacker, identity);
                await db.SaveChangesAsync(ct);
                stackerId = stacker.Id;
                identityId = identity.Id;
                db.StackerIdentityLinks.Add(new StackerIdentityLink
                {
                    StackerId = stackerId, SportStackerIdentityId = identityId,
                    MatchMethod = "Synthetic", ResolutionReasonCode = "SYNTHETIC_RECOVERY", LinkedAt = now
                });
                db.CompetitionStates.Add(new CompetitionState
                {
                    CompetitionKey = key, JsonData = stateJson, StateRevision = 4, SchemaVersion = "0.9-online",
                    CreatedAt = now, UpdatedAt = now
                });
                foreach (var stage in new[] { "Prelims", "Finals" })
                    db.CompetitionResults.Add(new CompetitionResult
                    {
                        CompetitionId = competitionId, Stage = stage, ParticipantType = "Individual",
                        ParticipantCode = "A1", EventCode = "Cycle", AttemptsJson = stage == "Finals" ? "[5.123]" : "[2.123]",
                        Revision = stage == "Finals" ? 2 : 1, CreatedAt = now, UpdatedAt = now
                    });
                db.CompetitionAssets.Add(new CompetitionAsset
                {
                    CompetitionId = competitionId, AssetType = "logo", FileName = "fixture.png",
                    StoredFileName = "fixture.png", ContentType = "image/png", FileSize = assetBytes.Length,
                    Sha256 = Digest(assetBytes), CreatedAt = now, UpdatedAt = now
                });
                await db.SaveChangesAsync(ct);
                var protectedSettings = new ProtectedSettingService(db, config, NullLogger<ProtectedSettingService>.Instance);
                await protectedSettings.Set(settingKey, secret, true, ct);
                cipher = await db.AppSettings.AsNoTracking().Where(x => x.Key == settingKey).Select(x => x.Value).SingleAsync(ct);
                Check(cipher.StartsWith("v2.", StringComparison.Ordinal) && cipher != secret, "authenticated encrypted setting created");
                sessionToken = new SessionTokenService(config).Create(key, "Synthetic recovery").ToString();
                competition.Status = "Closed";
                await db.SaveChangesAsync(ct);
                snapshot = await new FinalsRankingGovernanceService(db).CaptureFinalizedSnapshotAsync(competitionId, null, ct);
                Check(snapshot.HasSnapshot && snapshot.SnapshotJson is not null && snapshot.SnapshotSha256 == Digest(Encoding.UTF8.GetBytes(snapshot.SnapshotJson)), "source snapshot digest");
                counts = await Counts(db, ct);
                await using var conn = new SqlConnection(source.ConnectionString);
                await conn.OpenAsync(ct);
                await using var files = conn.CreateCommand();
                files.CommandText = "SELECT [name],[type] FROM sys.database_files ORDER BY [type],[file_id]";
                await using var reader = await files.ExecuteReaderAsync(ct);
                var names = new List<string>();
                var types = new List<byte>();
                while (await reader.ReadAsync(ct)) { names.Add(reader.GetString(0)); types.Add(Convert.ToByte(reader.GetValue(1))); }
                Check(names.Count == 2 && types.SequenceEqual(new byte[] { 0, 1 }), "exactly one data and one log file");
                logicalFiles = names.ToArray();
            }

            // All fixture writers are now quiescent. Capture one coordinated backup set.
            watch.Restart();
            var sourceDir = Path.Combine(root, "source");
            var backupDir = Path.Combine(root, "backup");
            var restoredDir = Path.Combine(root, "restored");
            foreach (var dir in new[] { sourceDir, backupDir, restoredDir }) Directory.CreateDirectory(dir);
            var relativeAsset = Path.Combine("assets", competitionId.ToString(System.Globalization.CultureInfo.InvariantCulture), "fixture.png");
            var relativeKey = Path.Combine("private", "synthetic-keys.json");
            var relativeConfig = Path.Combine("live", "web.config");
            var relativeRelease = Path.Combine("release", "app.js");
            WriteFixture(sourceDir, relativeAsset, assetBytes);
            WriteFixture(sourceDir, relativeKey, JsonSerializer.SerializeToUtf8Bytes(configurationValues));
            WriteFixture(sourceDir, relativeConfig, Encoding.UTF8.GetBytes("<configuration><!-- synthetic protected IIS fixture --></configuration>"));
            var appPath = Path.GetFullPath("backend/StackMeet.Api/wwwroot/app.js");
            Check(File.Exists(appPath), "reviewed checkout frontend file present");
            WriteFixture(sourceDir, relativeRelease, await File.ReadAllBytesAsync(appPath, ct));

            var paths = new[] { relativeAsset, relativeKey, relativeConfig, relativeRelease };
            var manifest = paths.ToDictionary(x => x, x => Digest(File.ReadAllBytes(Path.Combine(sourceDir, x))), StringComparer.Ordinal);
            foreach (var path in paths) CopyFixture(sourceDir, backupDir, path);
            var backupPath = Path.Combine(backupDir, "fixture.bak");
            await Exec(master, $"BACKUP DATABASE [{source.InitialCatalog}] TO DISK = {Literal(backupPath)} WITH COPY_ONLY, CHECKSUM, INIT", ct);
            await Exec(master, $"RESTORE VERIFYONLY FROM DISK = {Literal(backupPath)} WITH CHECKSUM", ct);
            var backupHash = Digest(await File.ReadAllBytesAsync(backupPath, ct));
            Check(new FileInfo(backupPath).Length > 0, "verified backup media exists");
            Check(backupHash == Digest(await File.ReadAllBytesAsync(backupPath, ct)), "backup media digest stable");
            var backupMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();

            // No REPLACE and no source file path reuse. Move both files into the generated temp root.
            await Exec(master, $"RESTORE DATABASE [{targetName}] FROM DISK = {Literal(backupPath)} WITH CHECKSUM, RECOVERY, MOVE {Literal(logicalFiles[0])} TO {Literal(Path.Combine(restoredDir, "fixture.mdf"))}, MOVE {Literal(logicalFiles[1])} TO {Literal(Path.Combine(restoredDir, "fixture.ldf"))}", ct);
            await using (var check = master.CreateCommand())
            {
                check.CommandText = $"DBCC CHECKDB ([{targetName}]) WITH NO_INFOMSGS, ALL_ERRORMSGS";
                check.CommandTimeout = 60;
                await using var reader = await check.ExecuteReaderAsync(ct);
                do
                {
                    Check(!await reader.ReadAsync(ct), "DBCC CHECKDB returned no error rows");
                } while (await reader.NextResultAsync(ct));
            }
            foreach (var path in paths) CopyFixture(backupDir, restoredDir, path);
            foreach (var path in paths)
                Check(manifest[path] == Digest(await File.ReadAllBytesAsync(Path.Combine(restoredDir, path), ct)), "restored manifest entry verified");

            // Exercise config exclusion on a synthetic live root, including a conflicting payload file.
            var simulatedLive = Path.Combine(root, "simulated-live");
            Directory.CreateDirectory(simulatedLive);
            CopyFixture(restoredDir, simulatedLive, relativeConfig);
            var protectedHash = Digest(await File.ReadAllBytesAsync(Path.Combine(simulatedLive, relativeConfig), ct));
            var simulatedPayload = Path.Combine(root, "simulated-payload");
            Directory.CreateDirectory(simulatedPayload);
            CopyFixture(restoredDir, simulatedPayload, relativeRelease);
            WriteFixture(simulatedPayload, relativeConfig, Encoding.UTF8.GetBytes("must never be copied"));
            // Explicit allowlist, not an actual IIS deployment or release-script replacement.
            CopyFixture(simulatedPayload, simulatedLive, relativeRelease);
            Check(protectedHash == Digest(await File.ReadAllBytesAsync(Path.Combine(simulatedLive, relativeConfig), ct)), "protected synthetic web.config unchanged by allowlisted copy");
            Check(manifest[relativeRelease] == Digest(await File.ReadAllBytesAsync(Path.Combine(simulatedLive, relativeRelease), ct)), "allowlisted release file matches manifest");

            var restoredValues = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllBytesAsync(Path.Combine(restoredDir, relativeKey), ct))
                ?? throw new InvalidOperationException("ENG04E missing recovered synthetic key material.");
            var restoredConfig = new ConfigurationBuilder().AddInMemoryCollection(restoredValues
                .Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build();
            await using (var db = Context(target.ConnectionString))
            {
                Check(counts.SequenceEqual(await Counts(db, ct)), "restored aggregate SQL counts");
                var restoredCompetition = await db.Competitions.AsNoTracking().SingleAsync(x => x.Id == competitionId, ct);
                Check(restoredCompetition.Status == "Closed" && restoredCompetition.IsPubliclyListed && restoredCompetition.ResultsRevision == 2, "restored lifecycle publication and result revision");
                var restoredState = await db.CompetitionStates.AsNoTracking().SingleAsync(x => x.CompetitionKey == key, ct);
                Check(restoredState.StateRevision == 4 && restoredState.JsonData == stateJson, "restored exact state and approved qualifier");
                Check(await db.Stackers.AsNoTracking().AnyAsync(x => x.Id == stackerId && x.CompetitionId == competitionId && x.StackerCode == "A1", ct), "restored participant");
                Check(await db.StackerIdentityLinks.AsNoTracking().AnyAsync(x => x.StackerId == stackerId && x.SportStackerIdentityId == identityId, ct)
                    && await db.SportStackerIdentities.AsNoTracking().AnyAsync(x => x.Id == identityId && !x.IsPublicProfile, ct), "restored identity link and privacy");
                var results = await db.CompetitionResults.AsNoTracking().Where(x => x.CompetitionId == competitionId).ToListAsync(ct);
                Check(results.Count == 2 && results.Single(x => x.Stage == "Finals").AttemptsJson == "[5.123]"
                    && results.Single(x => x.Stage == "Prelims").AttemptsJson == "[2.123]", "restored Finals and prelims remain distinct");
                var restoredSnapshot = await new FinalsRankingGovernanceService(db).GetAsync(competitionId, ct);
                Check(restoredSnapshot == snapshot && restoredSnapshot?.SnapshotJson is not null
                    && restoredSnapshot.SnapshotSha256 == Digest(Encoding.UTF8.GetBytes(restoredSnapshot.SnapshotJson)), "original certified snapshot bytes and metadata restored");
                using (var doc = JsonDocument.Parse(restoredSnapshot!.SnapshotJson!))
                    Check(doc.RootElement.GetProperty("finalsResults").GetArrayLength() == 1
                        && doc.RootElement.GetProperty("finalsResults")[0].GetProperty("attemptsJson").GetString() == "[5.123]", "restored historical Finals-only evidence");
                var asset = await db.CompetitionAssets.AsNoTracking().SingleAsync(x => x.CompetitionId == competitionId, ct);
                var file = await File.ReadAllBytesAsync(Path.Combine(restoredDir, relativeAsset), ct);
                Check(file.LongLength == asset.FileSize && Digest(file) == asset.Sha256, "restored SQL asset metadata matches bytes");
                var setting = await db.AppSettings.AsNoTracking().SingleAsync(x => x.Key == settingKey, ct);
                Check(setting.IsProtected && setting.Value == cipher, "restored exact protected ciphertext");
                var service = new ProtectedSettingService(db, restoredConfig, NullLogger<ProtectedSettingService>.Instance);
                Check(await service.Get(settingKey, ct) == secret, "recovered synthetic key decrypts setting");
                Check(new SessionTokenService(restoredConfig).TryValidate(sessionToken, out var parsed)
                    && parsed.CompetitionId == key, "recovered signing key validates synthetic session");
                Check((await db.AppSettings.AsNoTracking().SingleAsync(x => x.Key == settingKey, ct)).Value == cipher, "verification does not rewrite current-format ciphertext");
            }
            watch.Stop();
            Console.WriteLine("ENG04E_METRIC " + JsonSerializer.Serialize(new
            {
                backupAndVerificationMs = Math.Round(backupMs, 3),
                restoreAndVerificationMs = Math.Round(watch.Elapsed.TotalMilliseconds, 3),
                backupBytes = new FileInfo(backupPath).Length, competitions = counts[0], stackers = counts[1], results = counts[2],
                checks = "SQL checksum/restore/CHECKDB, asset manifest, protected keys/settings, snapshot, privacy, synthetic config exclusion"
            }));
            Console.WriteLine("PASS ENG04E coordinated synthetic recovery");
        }
        finally
        {
            // Close only this exact generated restore target. Never drop or overwrite the source.
            SqlConnection.ClearAllPools();
            if (ownsRestoreTarget)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await Exec(master, $"IF DB_ID({Literal(targetName)}) IS NOT NULL BEGIN ALTER DATABASE [{targetName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{targetName}]; END", cleanup.Token);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    private static StackMeetDbContext Context(string cs) =>
        new(new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(cs).Options);

    private static async Task<int[]> Counts(StackMeetDbContext db, CancellationToken ct) =>
        [await db.Competitions.CountAsync(ct), await db.Stackers.CountAsync(ct), await db.CompetitionResults.CountAsync(ct),
         await db.CompetitionStates.CountAsync(ct), await db.CompetitionAssets.CountAsync(ct),
         await db.SportStackerIdentities.CountAsync(ct), await db.StackerIdentityLinks.CountAsync(ct), await db.AppSettings.CountAsync(ct)];

    private static async Task Exec(SqlConnection connection, string commandText, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = 60;
        await using var reader = await command.ExecuteReaderAsync(ct);
        do { while (await reader.ReadAsync(ct)) { } } while (await reader.NextResultAsync(ct));
    }

    private static void WriteFixture(string root, string relative, byte[] bytes)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
    private static void CopyFixture(string from, string to, string relative) =>
        WriteFixture(to, relative, File.ReadAllBytes(Path.Combine(from, relative)));
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Literal(string value) => "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    private static bool SafeName(string name) => name.Length <= 128 && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ENG04E failed: " + message);
    }
}
