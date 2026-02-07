namespace MusicSugessionAppMVP_ASP.Services
{
    public interface ISoundCloudApiService
    {
        string GetAuthorizationUrl(string redirectUri);

        Task<string> ExchangeCodeForTokenAsync(
            string code,
            string redirectUri);

        Task<string> CreatePlaylistAsync(
            string accessToken,
            string title);

        Task<string> AddTrackToPlaylistAsync(
            string accessToken,
            long playlistId,
            long trackId);

        Task<long?> SearchTrackIdAsync(
            string trackName);
    }
}
