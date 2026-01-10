namespace MusicSugessionAppMVP_ASP.Models
{
    public class CrateSessionState
    {
        public Queue<TrackInfo> PrimaryQueue { get; } = new();
        public List<TrackInfo> BackupQueue { get; } = new();
        public HashSet<string> SeenArtists { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> SelectedGenres { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<TrackInfo> LikedTracks { get; } = new();
        public List<TrackInfo> SkippedTracks { get; } = new();

        public bool IsWarm { get; set; } // equivalent to "review window opened"
    }
}
