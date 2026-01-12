using MusicSugessionAppMVP_ASP.Persistance;

namespace MusicSugessionAppMVP_ASP.Models
{
    public class CrateSessionState
    {
        public Guid SessionId { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }

        public DeviceType DeviceType { get; set; }

        public Guid? UserIdSnapshot { get; set; }


        public Queue<TrackInfo> PrimaryQueue { get; } = new();
        public List<TrackInfo> BackupQueue { get; } = new();
        public HashSet<string> SeenArtists { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> SelectedGenres { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<TrackInfo> LikedTracks { get; } = new();
        public List<TrackInfo> SkippedTracks { get; } = new();

        public string? RegisteredEmail { get; set; }
        public DateTimeOffset? EmailRegisteredAtUtc { get; set; }
        public ExportMedium SelectedExportMedium { get; set; } = ExportMedium.None;
        public bool EmailSent { get; set; }   // ✅ NEW
        public bool IsWarm { get; set; } // equivalent to "review window opened"

        public int TotalSwipeCount { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }

        public HashSet<Guid> ExposedTrackIds { get; } = new();

        public List<SeedArtistSnapshot> SeedArtists { get; set; } = new();

        public DateTimeOffset? ExportedAtUtc { get; set; }

    }
}
