using MusicSugessionAppMVP_ASP.Models;

namespace MusicSugessionAppMVP_ASP.Services
{
    public class SimilarArtistAggregationService
    {
        private readonly AppleMusicService _appleMusicService;

        public SimilarArtistAggregationService(AppleMusicService appleMusicService)
        {
            _appleMusicService = appleMusicService;
        }

        public async Task<List<ArtistInfo>> GetSimilarArtistsWithAtLeastOneTrackAsync(
            string artistName,
            int maxArtists,
            CancellationToken cancellationToken = default)
        {
            return await _appleMusicService.GetSimilarArtistsWithAtLeastOneTrackAsync(
                artistName,
                maxArtists,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
