using Microsoft.EntityFrameworkCore;
using TournamentSystemAPI.Models;

namespace TournamentSystemAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Tournament> Tournaments { get; set; } = null!;
        public DbSet<Bracket> Brackets { get; set; } = null!;
        public DbSet<Match> Matches { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Match -> Player1 (restrict delete to avoid cascade cycles)
            modelBuilder.Entity<Match>()
                .HasOne(m => m.Player1)
                .WithMany()
                .HasForeignKey(m => m.Player1Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Match -> Player2 (restrict delete)
            modelBuilder.Entity<Match>()
                .HasOne(m => m.Player2)
                .WithMany()
                .HasForeignKey(m => m.Player2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Match -> Winner (restrict delete)
            modelBuilder.Entity<Match>()
                .HasOne(m => m.Winner)
                .WithMany()
                .HasForeignKey(m => m.WinnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Match -> Bracket (many matches to one bracket)
            modelBuilder.Entity<Match>()
                .HasOne(m => m.Bracket)
                .WithMany(b => b.Matches)
                .HasForeignKey(m => m.BracketId);

            // Configure Tournament <-> User many-to-many (Participants) using a join table
            modelBuilder.Entity<Tournament>()
                .HasMany(t => t.Participants)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "TournamentParticipant",
                    j => j.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<Tournament>().WithMany().HasForeignKey("TournamentId").OnDelete(DeleteBehavior.Cascade)
                );

            // Configure Tournament 1-to-1 Bracket
            modelBuilder.Entity<Tournament>()
                .HasOne(t => t.Bracket)
                .WithOne(b => b.Tournament)
                .HasForeignKey<Bracket>(b => b.TournamentId);
        }
    }
}
