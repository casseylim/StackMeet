using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Models;
using StackMeet.Api.Services;

namespace StackMeet.Api.Data;

public sealed class StackMeetDbContext(DbContextOptions<StackMeetDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();
    public DbSet<CompetitionUser> CompetitionUsers => Set<CompetitionUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppUserToken> AppUserTokens => Set<AppUserToken>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<CompetitionState> CompetitionStates => Set<CompetitionState>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Stacker> Stackers => Set<Stacker>();
    public DbSet<CompetitionResult> CompetitionResults => Set<CompetitionResult>();
    public DbSet<CompetitionAsset> CompetitionAssets => Set<CompetitionAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var appUser = modelBuilder.Entity<AppUser>();
        appUser.ToTable("AppUser", "dbo");
        appUser.HasKey(item => item.Id);
        appUser.Property(item => item.Id).UseIdentityColumn();
        appUser.Property(item => item.Email).HasMaxLength(200).IsRequired();
        appUser.Property(item => item.NormalizedEmail).HasMaxLength(200).IsRequired();
        appUser.HasIndex(item => item.NormalizedEmail).IsUnique();
        appUser.Property(item => item.PasswordHash).HasMaxLength(500).IsRequired();
        appUser.Property(item => item.DisplayName).HasMaxLength(150).IsRequired();
        appUser.Property(item => item.IsActive).IsRequired().HasDefaultValue(true);
        appUser.Property(item => item.EmailConfirmed).IsRequired().HasDefaultValue(false);
        appUser.Property(item => item.IsSystemAdmin).IsRequired().HasDefaultValue(false);
        appUser.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        appUser.Property(item => item.LastLoginAt).HasColumnType("datetime2");
        appUser.Property(item => item.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
        appUser.Property(item => item.LoginLockoutRound).IsRequired().HasDefaultValue(0);
        appUser.Property(item => item.LockoutUntil).HasColumnType("datetime2");
        appUser.Property(item => item.IsPermanentlyLocked).IsRequired().HasDefaultValue(false);

        var appUserToken = modelBuilder.Entity<AppUserToken>();
        appUserToken.ToTable("AppUserToken", "dbo");
        appUserToken.HasKey(item => item.Id);
        appUserToken.Property(item => item.Id).UseIdentityColumn();
        appUserToken.Property(item => item.Purpose).HasMaxLength(50).IsRequired();
        appUserToken.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
        appUserToken.Property(item => item.ExpiresAt).HasColumnType("datetime2").IsRequired();
        appUserToken.Property(item => item.UsedAt).HasColumnType("datetime2");
        appUserToken.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        appUserToken.HasIndex(item => item.TokenHash).IsUnique();
        appUserToken.HasIndex(item => new { item.UserId, item.Purpose, item.UsedAt });
        appUserToken.HasOne(item => item.User).WithMany(item => item.Tokens).HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);

        var appSetting = modelBuilder.Entity<AppSetting>();
        appSetting.ToTable("AppSetting", "dbo");
        appSetting.HasKey(item => item.Id);
        appSetting.Property(item => item.Id).UseIdentityColumn();
        appSetting.Property(item => item.Key).HasMaxLength(150).IsRequired();
        appSetting.HasIndex(item => item.Key).IsUnique();
        appSetting.Property(item => item.Value).HasColumnType("nvarchar(max)").IsRequired();
        appSetting.Property(item => item.IsProtected).IsRequired().HasDefaultValue(false);
        appSetting.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();

        var appRole = modelBuilder.Entity<AppRole>();
        appRole.ToTable("AppRole", "dbo");
        appRole.HasKey(item => item.Id);
        appRole.Property(item => item.Id).ValueGeneratedNever();
        appRole.Property(item => item.Name).HasMaxLength(50).IsRequired();
        appRole.HasIndex(item => item.Name).IsUnique();
        appRole.HasData(
            new AppRole { Id = 1, Name = StackMeetRoles.SystemAdmin },
            new AppRole { Id = 2, Name = StackMeetRoles.CompetitionManager },
            new AppRole { Id = 3, Name = StackMeetRoles.DataEntry },
            new AppRole { Id = 4, Name = StackMeetRoles.Viewer });

        var competitionUser = modelBuilder.Entity<CompetitionUser>();
        competitionUser.ToTable("CompetitionUser", "dbo");
        competitionUser.HasKey(item => item.Id);
        competitionUser.Property(item => item.Id).UseIdentityColumn();
        competitionUser.Property(item => item.IsActive).IsRequired().HasDefaultValue(true);
        competitionUser.Property(item => item.AssignedAt).HasColumnType("datetime2").IsRequired();
        competitionUser.HasIndex(item => new { item.CompetitionId, item.UserId }).IsUnique();
        competitionUser.HasOne(item => item.Competition).WithMany(item => item.CompetitionUsers).HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.Restrict);
        competitionUser.HasOne(item => item.User).WithMany(item => item.CompetitionUsers).HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        competitionUser.HasOne(item => item.Role).WithMany(item => item.CompetitionUsers).HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Restrict);
        competitionUser.HasOne(item => item.AssignedByUser).WithMany().HasForeignKey(item => item.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);

        var auditLog = modelBuilder.Entity<AuditLog>();
        auditLog.ToTable("AuditLog", "dbo");
        auditLog.HasKey(item => item.Id);
        auditLog.Property(item => item.Id).UseIdentityColumn();
        auditLog.Property(item => item.Action).HasMaxLength(100).IsRequired();
        auditLog.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
        auditLog.Property(item => item.EntityId).HasMaxLength(100);
        auditLog.Property(item => item.OldValueJson).HasColumnType("nvarchar(max)");
        auditLog.Property(item => item.NewValueJson).HasColumnType("nvarchar(max)");
        auditLog.Property(item => item.IpAddress).HasMaxLength(100);
        auditLog.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        auditLog.HasIndex(item => new { item.CompetitionId, item.CreatedAt });
        auditLog.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.SetNull);
        auditLog.HasOne(item => item.Competition).WithMany(item => item.AuditLogs).HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.SetNull);

        var competitionState = modelBuilder.Entity<CompetitionState>();
        competitionState.ToTable("CompetitionState", "dbo");
        competitionState.HasKey(state => state.Id);
        competitionState.Property(state => state.Id).UseIdentityColumn();
        competitionState.HasIndex(state => state.CompetitionKey).IsUnique();
        competitionState.Property(state => state.CompetitionKey).HasMaxLength(100).IsRequired();
        competitionState.Property(state => state.JsonData).HasColumnType("nvarchar(max)").IsRequired();
        competitionState.Property(state => state.SchemaVersion).HasMaxLength(50).IsRequired().HasDefaultValue("0.9-online");
        competitionState.Property(state => state.CreatedAt).HasColumnType("datetime2").IsRequired();
        competitionState.Property(state => state.UpdatedAt).HasColumnType("datetime2").IsRequired();
        competitionState.Property(state => state.UpdatedBy).HasMaxLength(100);

        var competition = modelBuilder.Entity<Competition>();
        competition.ToTable("Competition", "dbo");
        competition.HasKey(item => item.Id);
        competition.Property(item => item.Id).UseIdentityColumn();
        competition.Property(item => item.CompetitionCode).HasMaxLength(50).IsRequired();
        competition.HasIndex(item => item.CompetitionCode).IsUnique();
        competition.Property(item => item.CompetitionKey).HasMaxLength(100).IsRequired();
        competition.HasIndex(item => item.CompetitionKey).IsUnique();
        competition.Property(item => item.CompetitionName).HasMaxLength(200).IsRequired();
        competition.Property(item => item.Venue).HasMaxLength(200).IsRequired();
        competition.Property(item => item.Status).HasMaxLength(30).IsRequired();
        competition.Property(item => item.IsPubliclyListed).IsRequired().HasDefaultValue(false);
        competition.Property(item => item.ResultsRevision).IsRequired().HasDefaultValue(0L);
        competition.Property(item => item.PasswordHash).HasMaxLength(500);
        competition.Property(item => item.ArchivedAt).HasColumnType("datetime2");
        competition.Property(item => item.ArchivedBy).HasMaxLength(100);
        competition.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        competition.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();

        var stacker = modelBuilder.Entity<Stacker>();
        stacker.ToTable("Stacker", "dbo");
        stacker.HasKey(item => item.Id);
        stacker.Property(item => item.Id).UseIdentityColumn();
        stacker.Property(item => item.StackerCode).HasMaxLength(50).IsRequired();
        stacker.Property(item => item.WssaId).HasMaxLength(50);
        stacker.Property(item => item.FirstName).HasMaxLength(100).IsRequired();
        stacker.Property(item => item.LastName).HasMaxLength(100).IsRequired();
        stacker.Property(item => item.Gender).HasMaxLength(20).IsRequired();
        stacker.Property(item => item.Country).HasMaxLength(100).IsRequired();
        stacker.Property(item => item.Club).HasMaxLength(200);
        stacker.Property(item => item.Region).HasMaxLength(100);
        stacker.Property(item => item.Email).HasMaxLength(200);
        stacker.Property(item => item.Phone).HasMaxLength(50);
        stacker.Property(item => item.CustomDivision).HasMaxLength(100);
        stacker.Property(item => item.Paid).HasMaxLength(10).IsRequired().HasDefaultValue("No");
        stacker.Property(item => item.CheckedIn).HasMaxLength(10).IsRequired().HasDefaultValue("No");
        stacker.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        stacker.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();
        stacker.HasIndex(item => new { item.CompetitionId, item.StackerCode }).IsUnique();
        stacker.HasOne(item => item.Competition).WithMany(item => item.Stackers).HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.Restrict);

        var result = modelBuilder.Entity<CompetitionResult>();
        result.ToTable("CompetitionResult", "dbo");
        result.HasKey(item => item.Id);
        result.Property(item => item.Id).UseIdentityColumn();
        result.Property(item => item.PublicId).IsRequired();
        result.HasIndex(item => item.PublicId).IsUnique();
        result.Property(item => item.Stage).HasMaxLength(30).IsRequired();
        result.Property(item => item.ParticipantType).HasMaxLength(30).IsRequired();
        result.Property(item => item.ParticipantCode).HasMaxLength(50).IsRequired();
        result.Property(item => item.EventCode).HasMaxLength(50).IsRequired();
        result.Property(item => item.AttemptsJson).HasColumnType("nvarchar(max)").IsRequired();
        result.Property(item => item.Penalty).HasColumnType("decimal(12,3)").IsRequired();
        result.Property(item => item.Revision).IsRequired();
        result.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        result.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();
        result.HasIndex(item => new { item.CompetitionId, item.Stage, item.ParticipantType, item.ParticipantCode, item.EventCode }).IsUnique().HasDatabaseName("UX_CompetitionResult_LogicalResult");
        result.HasOne(item => item.Competition).WithMany(item => item.Results).HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.Cascade);

        var asset = modelBuilder.Entity<CompetitionAsset>();
        asset.ToTable("CompetitionAsset", "dbo");
        asset.HasKey(item => item.Id);
        asset.Property(item => item.Id).UseIdentityColumn();
        asset.Property(item => item.AssetType).HasMaxLength(30).IsRequired();
        asset.Property(item => item.FileName).HasMaxLength(255).IsRequired();
        asset.Property(item => item.StoredFileName).HasMaxLength(255).IsRequired();
        asset.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
        asset.Property(item => item.FileSize).IsRequired();
        asset.Property(item => item.Sha256).HasMaxLength(64).IsRequired();
        asset.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        asset.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();
        asset.HasIndex(item => new { item.CompetitionId, item.AssetType }).IsUnique();
        asset.HasOne(item => item.Competition).WithMany(item => item.Assets).HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}
