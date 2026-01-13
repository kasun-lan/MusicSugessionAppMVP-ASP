using Microsoft.EntityFrameworkCore;

namespace MusicSugessionAppMVP_ASP.Persistance
{
    public class Persistence : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(
                    "Server=(localdb)\\mssqllocaldb;Database=MusicDiscovery;Trusted_Connection=True;TrustServerCertificate=True;"
                );
            }
        }

        // -----------------------------
        // DbSets
        // -----------------------------

        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<SessionExport> SessionExports => Set<SessionExport>();
        public DbSet<SessionInputArtist> SessionInputArtists => Set<SessionInputArtist>();
        public DbSet<SwipeEvent> SwipeEvents => Set<SwipeEvent>();

        public DbSet<User> Users => Set<User>();

        public DbSet<Artist> Artists => Set<Artist>();
        public DbSet<Track> Tracks => Set<Track>();
        public DbSet<Genre> Genres => Set<Genre>();
        public DbSet<TrackGenre> TrackGenres => Set<TrackGenre>();

        public DbSet<TrackExposure> TrackExposures => Set<TrackExposure>();
        public DbSet<ArtistExposure> ArtistExposures => Set<ArtistExposure>();
        public DbSet<GenreExposure> GenreExposures => Set<GenreExposure>();

        public DbSet<SessionStats> SessionStats => Set<SessionStats>();
        public DbSet<SessionStatsSnapshot> SessionStatsSnapshots => Set<SessionStatsSnapshot>();
        public DbSet<TrackStats> TrackStats => Set<TrackStats>();

        public DbSet<ArtistSimilarityApi> ArtistSimilarityApis => Set<ArtistSimilarityApi>();
        public DbSet<ArtistSimilarityInternal> ArtistSimilarityInternals => Set<ArtistSimilarityInternal>();
        public DbSet<ArtistNoSimilarFound> ArtistNoSimilarFounds => Set<ArtistNoSimilarFound>();

        public DbSet<StreamingPlatform> StreamingPlatforms => Set<StreamingPlatform>();
        public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

        // -----------------------------
        // Model Configuration
        // -----------------------------

        protected override void OnModelCreating(ModelBuilder model)
        {
            base.OnModelCreating(model);

            // -----------------------------
            // Session
            // -----------------------------
            model.Entity<Session>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.User)
                 .WithMany(u => u.Sessions)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.Property(x => x.DeviceType)
                 .HasConversion<int>();
            });

            // -----------------------------
            // SessionExport
            // -----------------------------
            model.Entity<SessionExport>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Session)
                 .WithMany()
                 .HasForeignKey(x => x.SessionId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(x => x.Medium)
                 .HasConversion<int>();
            });

            // -----------------------------
            // SessionInputArtist
            // -----------------------------
            model.Entity<SessionInputArtist>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Session)
                 .WithMany(s => s.InputArtists)
                 .HasForeignKey(x => x.SessionId);

                e.HasOne(x => x.Artist)
                 .WithMany()
                 .HasForeignKey(x => x.ArtistId);
            });

            // -----------------------------
            // SwipeEvent
            // -----------------------------
            model.Entity<SwipeEvent>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Session)
                 .WithMany(s => s.SwipeEvents)
                 .HasForeignKey(x => x.SessionId);

                e.HasOne(x => x.Track)
                 .WithMany()
                 .HasForeignKey(x => x.TrackId);

                e.Property(x => x.Direction)
                 .HasConversion<int>();
            });

            // -----------------------------
            // Artist
            // -----------------------------
            model.Entity<Artist>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Name);
            });

            // -----------------------------
            // Track
            // -----------------------------
            model.Entity<Track>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Artist)
                 .WithMany(a => a.Tracks)
                 .HasForeignKey(x => x.ArtistId);
            });

            // -----------------------------
            // Genre
            // -----------------------------
            model.Entity<Genre>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Name).IsUnique();
            });

            // -----------------------------
            // TrackGenre (Many-to-Many)
            // -----------------------------
            model.Entity<TrackGenre>(e =>
            {
                e.HasKey(x => new { x.TrackId, x.GenreId });

                e.HasOne(x => x.Track)
                 .WithMany(t => t.Genres)
                 .HasForeignKey(x => x.TrackId);

                e.HasOne(x => x.Genre)
                 .WithMany(g => g.Tracks)
                 .HasForeignKey(x => x.GenreId);
            });

            // -----------------------------
            // Exposure Tables
            // -----------------------------
            model.Entity<TrackExposure>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Session)
                 .WithMany()
                 .HasForeignKey(x => x.SessionId);

                e.HasOne(x => x.Track)
                 .WithMany()
                 .HasForeignKey(x => x.TrackId);
            });

            model.Entity<ArtistExposure>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Session)
                 .WithMany()
                 .HasForeignKey(x => x.SessionId);

                e.HasOne(x => x.Artist)
                 .WithMany()
                 .HasForeignKey(x => x.ArtistId);
            });

            model.Entity<GenreExposure>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Session)
                 .WithMany()
                 .HasForeignKey(x => x.SessionId);

                e.HasOne(x => x.Genre)
                 .WithMany()
                 .HasForeignKey(x => x.GenreId);
            });

            // -----------------------------
            // Stats (One-row-per-entity)
            // -----------------------------
            model.Entity<SessionStats>(e =>
            {
                e.HasKey(x => x.SessionId);
            });

            model.Entity<SessionStatsSnapshot>(e =>
            {
                e.HasKey(x => new { x.SessionId, x.CapturedAtUtc });
            });

            model.Entity<TrackStats>(e =>
            {
                e.HasKey(x => x.TrackId);
            });

            // -----------------------------
            // Similarity Tables
            // -----------------------------
            model.Entity<ArtistSimilarityApi>(e =>
            {
                e.HasKey(x => x.Id);
            });

            model.Entity<ArtistSimilarityInternal>(e =>
            {
                e.HasKey(x => x.Id);
            });

            model.Entity<ArtistNoSimilarFound>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.Artist)
                 .WithMany()
                 .HasForeignKey(x => x.ArtistId);
            });

            // -----------------------------
            // Streaming Platform
            // -----------------------------
            model.Entity<StreamingPlatform>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Name).IsUnique();
            });

            // -----------------------------
            // User
            // -----------------------------
            model.Entity<User>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.PreferredStreamingPlatform)
                 .WithMany()
                 .HasForeignKey(x => x.PreferredStreamingPlatformId);
            });

            // -----------------------------
            // UserRoleAssignment
            // -----------------------------
            model.Entity<UserRoleAssignment>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(x => x.User)
                 .WithMany(u => u.Roles)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(x => x.Role)
                 .HasConversion<int>();
            });
        }

    }
}
