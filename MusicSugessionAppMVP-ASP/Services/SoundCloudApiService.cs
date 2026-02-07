using System;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MusicSugessionAppMVP_ASP.Services
{
    public class SoundCloudApiService : ISoundCloudApiService
    {


        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        public SoundCloudApiService(
            HttpClient httpClient,
            string clientId,
            string clientSecret)
        {
            _httpClient = httpClient;
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        public string GetAuthorizationUrl(string redirectUri)
        {
            return
                $"https://soundcloud.com/connect?" +
                $"client_id={_clientId}" +
                $"&redirect_uri={redirectUri}" +
                $"&response_type=code" +
                $"&scope=non-expiring";
        }

        public async Task<string> ExchangeCodeForTokenAsync(
            string code,
            string redirectUri)
        {
            var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret),
            new KeyValuePair<string, string>("redirect_uri", redirectUri),
            new KeyValuePair<string, string>("code", code)
        });

            var response = await _httpClient.PostAsync(
                "https://api.soundcloud.com/oauth2/token",
                content);

            response.EnsureSuccessStatusCode();

            var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            return json.RootElement
                .GetProperty("access_token")
                .GetString();
        }

        public async Task<string> CreatePlaylistAsync(
            string accessToken,
            string title)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.soundcloud.com/playlists");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("OAuth", accessToken);

            request.Content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("playlist[title]", title)
        });

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> AddTrackToPlaylistAsync(
            string accessToken,
            long playlistId,
            long trackId)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"https://api.soundcloud.com/playlists/{playlistId}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("OAuth", accessToken);

            request.Content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("playlist[tracks][]", trackId.ToString())
        });

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<long?> SearchTrackIdAsync(
            string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName))
                return null;

            // SoundCloud search API endpoint
            // Note: SoundCloud's public search API uses client_id, not OAuth
            var url = $"https://api.soundcloud.com/tracks" +
                      $"?client_id={_clientId}" +
                      $"&q={Uri.EscapeDataString(trackName)}" +
                      $"&limit=5";

            try
            {
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var jsonString = await response.Content.ReadAsStringAsync();
                using var json = JsonDocument.Parse(jsonString);
                
                // SoundCloud returns an array of tracks
                if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
                    return null;

                // Try to find the best match by comparing track name
                var normalizedTrackName = trackName.ToLowerInvariant();
                
                foreach (var track in json.RootElement.EnumerateArray())
                {
                    if (!track.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
                        continue;

                    var trackId = idEl.GetInt64();
                    
                    // Get track title for matching
                    var title = track.TryGetProperty("title", out var titleEl) 
                        ? titleEl.GetString()?.ToLowerInvariant() ?? "" 
                        : "";

                    // Simple matching: check if track name matches
                    if (title.Contains(normalizedTrackName, StringComparison.OrdinalIgnoreCase))
                    {
                        return trackId;
                    }
                }

                // If no exact match, return the first result (best effort)
                var firstTrack = json.RootElement[0];
                if (firstTrack.TryGetProperty("id", out var firstIdEl) && firstIdEl.ValueKind == JsonValueKind.Number)
                {
                    return firstIdEl.GetInt64();
                }

                return null;
            }
            catch (Exception)
            {
                // Log error if needed
                return null;
            }
        }
    }
}
