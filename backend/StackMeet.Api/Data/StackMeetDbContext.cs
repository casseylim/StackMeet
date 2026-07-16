using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Models;

namespace StackMeet.Api.Data;

public sealed class StackMeetDbContext(DbContextOptions<StackMeetDbContext> options) : DbContext(options)
{
    public DbSet<CompetitionState> CompetitionStates => Set<CompetitionState>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Stacker> Stackers => Set<Stacker>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}