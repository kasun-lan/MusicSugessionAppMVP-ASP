using System.Collections.Concurrent;

namespace MusicSugessionAppMVP_ASP.Models
{
    public class RecommendationSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();

        public HashSet<string> SeedArtistIds { get; } = new();
        public HashSet<string> ProcessedArtistIds { get; } = new();

        public ConcurrentQueue<TrackDto> TrackQueue { get; } = new();

        public bool IsBackgroundWorkerStarted { get; set; }
    }
}
