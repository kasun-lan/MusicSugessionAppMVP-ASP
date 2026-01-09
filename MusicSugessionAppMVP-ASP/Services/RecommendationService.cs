using MusicSugessionAppMVP_ASP.Models;

namespace MusicSugessionAppMVP_ASP.Services
{
    public class RecommendationService
    {
        private readonly SpotifyClient _spotify;

        public RecommendationService(SpotifyClient spotify)
        {
            _spotify = spotify;
        }

        public async Task FillQueueAsync(
            RecommendationSession session,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (session.TrackQueue.Count >= 20)
                {
                    await Task.Delay(500, ct);
                    continue;
                }

                var nextArtistId = GetNextArtist(session);
                if (nextArtistId == null)
                    break;

                var relatedArtists =
                    await _spotify.Artists.GetRelatedArtists(nextArtistId);

                foreach (var artist in relatedArtists.Artists)
                {
                    if (!session.ProcessedArtistIds.Add(artist.Id))
                        continue;

                    var topTracks =
                        await _spotify.Artists.GetTopTracks(
                            artist.Id,
                            new ArtistsTopTracksRequest("US"));

                    foreach (var track in topTracks.Tracks)
                    {
                        session.TrackQueue.Enqueue(Map(track));
                    }

                    if (session.TrackQueue.Count >= 5)
                        break;
                }
            }
        }

        private string? GetNextArtist(RecommendationSession session)
        {
            return session.SeedArtistIds
                .FirstOrDefault(id => !session.ProcessedArtistIds.Contains(id));
        }

        private TrackDto Map(FullTrack track)
        {
            return new TrackDto
            {
                Id = track.Id,
                Name = track.Name,
                Artist = track.Artists.First().Name,
                PreviewUrl = track.PreviewUrl,
                ImageUrl = track.Album.Images.FirstOrDefault()?.Url
            };
        }
    }
}
