using MusicSugessionAppMVP_ASP.Persistance;

namespace MusicSugessionAppMVP_ASP.Models
{
    public class CrateSessionState
    {
            // Identity & lifecycle
            public Guid SessionId { get; set; }
            public DateTime StartedAtUtc { get; set; }
            public DateTime? EndedAtUtc { get; set; }
            public Guid? UserIdSnapshot { get; set; }
            public DeviceType DeviceType { get; set; }

            // Queues
            public Queue<TrackInfo> PrimaryQueue { get; } = new();
            public List<TrackInfo> BackupQueue { get; } = new();

            // Discovery tracking
            public HashSet<string> SeenArtists { get; } =
                new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SelectedGenres { get; set; } =
                new(StringComparer.OrdinalIgnoreCase);

            // Seed artists
            public List<SeedArtistSnapshot> SeedArtists { get; set; } = new();

            // Interaction tracking
            public List<TrackInfo> LikedTracks { get; } = new();
            public List<TrackInfo> SkippedTracks { get; } = new();
            public int TotalSwipeCount { get; set; }
            public int LikeCount { get; set; }
            public int DislikeCount { get; set; }

            // Exposure guard
            public HashSet<Guid> ExposedTrackIds { get; } = new();

            // Export
            public string? RegisteredEmail { get; set; }
            public DateTimeOffset? EmailRegisteredAtUtc { get; set; }
            public ExportMedium SelectedExportMedium { get; set; }
            public bool EmailSent { get; set; }
            public DateTimeOffset? ExportedAtUtc { get; set; }

            // UI flow
            public bool IsWarm { get; set; }

            public CrateLifecycleState LifecycleState { get; set; }



    }
}
