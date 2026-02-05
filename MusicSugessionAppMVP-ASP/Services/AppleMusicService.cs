using System.Net.Http.Headers;
using System.Text.Json;
using MusicSugessionAppMVP_ASP.Models;

namespace MusicSugessionAppMVP_ASP.Services
{
    /// <summary>
    /// Minimal Apple Music API client (Catalog-focused) that can replace the current
    /// Spotify/Deezer/MusicBrainz calls used by this app (artist search, genres,
    /// related artists, top songs, previews).
    /// </summary>
    public class AppleMusicService
    {
        private readonly HttpClient _httpClient;
        private readonly AppleMusicTokenService _tokenService;
        private readonly string _storefront;

        public AppleMusicService(
            HttpClient httpClient,
            AppleMusicTokenService tokenService,
            IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));

            _storefront = configuration["AppleMusic:Storefront"];
            if (string.IsNullOrWhiteSpace(_storefront))
                _storefront = "us";

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri("https://api.music.apple.com/v1/");

            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Escape hatch for "any endpoint". Pass relative path under /v1 (e.g. "catalog/us/search?...").
        /// </summary>
        public async Task<JsonDocument> GetRawAsync(string relativePathAndQuery, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Get, relativePathAndQuery);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<ArtistInfo>> SearchArtistsAsync(
            string query,
            int limit = 5,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<ArtistInfo>();

            var url =
                $"catalog/{_storefront}/search" +
                $"?term={Uri.EscapeDataString(query)}" +
                $"&types=artists" +
                $"&limit={limit}";

            using var request = CreateRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            var result = new List<ArtistInfo>();

            if (!json.RootElement.TryGetProperty("results", out var results))
                return result;

            if (!results.TryGetProperty("artists", out var artistsObj))
                return result;

            if (!artistsObj.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in data.EnumerateArray())
                result.Add(ParseArtist(item, source: "AppleMusic:Search"));

            return result;
        }

        public async Task<ArtistInfo?> GetArtistByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var results = await SearchArtistsAsync(name, limit: 5, cancellationToken).ConfigureAwait(false);
            return results.FirstOrDefault();
        }

        public async Task<ArtistInfo?> GetArtistByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var url = $"catalog/{_storefront}/artists/{Uri.EscapeDataString(id)}";

            using var request = CreateRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                return null;

            return ParseArtist(data[0], source: "AppleMusic:Artist");
        }

        public async Task<List<ArtistInfo>> GetRelatedArtistsAsync(
            string artistId,
            int limit = 25,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(artistId))
                return new List<ArtistInfo>();

            // Apple Music docs are sometimes hard to access; different sources reference slightly different paths.
            // We try a small set of plausible endpoints.
            var candidatePaths = new[]
            {
                $"catalog/{_storefront}/artists/{Uri.EscapeDataString(artistId)}/related-artists?limit={limit}",
                $"catalog/{_storefront}/artists/{Uri.EscapeDataString(artistId)}/related?limit={limit}",
                $"catalog/{_storefront}/artists/{Uri.EscapeDataString(artistId)}/view/related-artists?limit={limit}",
                $"catalog/{_storefront}/artists/{Uri.EscapeDataString(artistId)}/view/similar-artists?limit={limit}",
            };

            using var json = await TryGetFirstSuccessfulJsonAsync(candidatePaths, cancellationToken).ConfigureAwait(false);
            if (json == null)
                return new List<ArtistInfo>();

            var data = ExtractPrimaryDataArray(json.RootElement);
            if (data == null)
                return new List<ArtistInfo>();

            var artists = new List<ArtistInfo>();
            foreach (var item in data.Value.EnumerateArray())
                artists.Add(ParseArtist(item, source: "AppleMusic:Related"));

            return artists;
        }

        public async Task<List<TrackInfo>> GetArtistTopTracksAsync(
            string artistId,
            int limit = 10,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(artistId))
                return new List<TrackInfo>();

            var candidatePaths = new[]
            {
                $"catalog/{_storefront}/artists/{Uri.EscapeDataString(artistId)}/view/top-songs?limit={limit}",
                $"catalog/{_storefront}/artists/{Uri.EscapeDataString(artistId)}/top-songs?limit={limit}",
                $"catalog/{_storefront}/artists/{Uri.EscapeDataString(artistId)}/view/top-tracks?limit={limit}",
            };

            using var json = await TryGetFirstSuccessfulJsonAsync(candidatePaths, cancellationToken).ConfigureAwait(false);
            if (json == null)
                return new List<TrackInfo>();

            var data = ExtractPrimaryDataArray(json.RootElement);
            if (data == null)
                return new List<TrackInfo>();

            var tracks = new List<TrackInfo>();
            foreach (var item in data.Value.EnumerateArray())
                tracks.Add(ParseSong(item));

            return tracks;
        }

        public async Task<string?> GetTrackPreviewUrlAsync(
            string trackName,
            string artistName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(trackName) || string.IsNullOrWhiteSpace(artistName))
                return null;

            var term = $"{artistName} {trackName}";

            var url =
                $"catalog/{_storefront}/search" +
                $"?term={Uri.EscapeDataString(term)}" +
                $"&types=songs" +
                $"&limit=1";

            using var request = CreateRequest(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!json.RootElement.TryGetProperty("results", out var results))
                return null;

            if (!results.TryGetProperty("songs", out var songsObj))
                return null;

            if (!songsObj.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                return null;

            var song = data[0];
            if (!TryGetAttributes(song, out var attributes))
                return null;

            if (attributes.TryGetProperty("previews", out var previews) &&
                previews.ValueKind == JsonValueKind.Array &&
                previews.GetArrayLength() > 0)
            {
                var first = previews[0];
                if (first.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                {
                    var previewUrl = urlEl.GetString();
                    return string.IsNullOrWhiteSpace(previewUrl) ? null : previewUrl;
                }
            }

            return null;
        }

        /// <summary>
        /// Equivalent to the current Deezer-based "similar artists + top tracks" aggregation.
        /// </summary>
        public async Task<List<ArtistInfo>> GetSimilarArtistsWithAtLeastOneTrackAsync(
            string seedArtistName,
            int maxArtists,
            CancellationToken cancellationToken = default)
        {
            var seed = await GetArtistByNameAsync(seedArtistName, cancellationToken).ConfigureAwait(false);
            if (seed == null || !TryGetAppleId(seed, out var seedId))
                return new List<ArtistInfo>();

            var related = await GetRelatedArtistsAsync(seedId, limit: Math.Max(10, maxArtists), cancellationToken).ConfigureAwait(false);
            var result = new List<ArtistInfo>();

            foreach (var artist in related)
            {
                if (result.Count >= maxArtists)
                    break;

                if (!TryGetAppleId(artist, out var artistId))
                    continue;

                var tracks = await GetArtistTopTracksAsync(artistId, limit: 3, cancellationToken).ConfigureAwait(false);
                if (tracks.Count < 1)
                    continue;

                artist.Tracks.AddRange(tracks);
                artist.TopTrackCount = tracks.Count;
                artist.Metadata["track_count"] = tracks.Count.ToString();

                result.Add(artist);
            }

            return result;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string relativePathAndQuery)
        {
            var req = new HttpRequestMessage(method, relativePathAndQuery);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenService.GenerateDeveloperToken());
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return req;
        }

        private async Task<JsonDocument?> TryGetFirstSuccessfulJsonAsync(IEnumerable<string> candidatePaths, CancellationToken cancellationToken)
        {
            foreach (var path in candidatePaths)
            {
                try
                {
                    using var request = CreateRequest(HttpMethod.Get, path);
                    using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Try next candidate path.
                }
            }

            return null;
        }

        private static JsonElement? ExtractPrimaryDataArray(JsonElement root)
        {
            // Common Apple Music API response shapes:
            // - { "data": [ ... ] }
            // - { "data": [ { "relationships": { "songs": { "data": [...] }}}]}
            // - { "results": { "<type>": { "data": [...] } } }

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                return data;

            if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Object)
            {
                // If "results" contains a single key, take its "data"
                foreach (var prop in results.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object &&
                        prop.Value.TryGetProperty("data", out var innerData) &&
                        innerData.ValueKind == JsonValueKind.Array)
                    {
                        return innerData;
                    }
                }
            }

            return null;
        }

        private static ArtistInfo ParseArtist(JsonElement item, string source)
        {
            var artist = new ArtistInfo
            {
                Name = TryGetAttributes(item, out var attributes) &&
                       attributes.TryGetProperty("name", out var nameEl) &&
                       nameEl.ValueKind == JsonValueKind.String
                    ? nameEl.GetString() ?? string.Empty
                    : string.Empty,
                Source = source
            };

            if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                artist.Metadata["apple_music_id"] = idEl.GetString();

            if (TryGetAttributes(item, out attributes))
            {
                if (attributes.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    artist.Metadata["apple_music_url"] = urlEl.GetString();

                if (attributes.TryGetProperty("genreNames", out var genreNames) && genreNames.ValueKind == JsonValueKind.Array)
                {
                    var names = genreNames.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (names.Count > 0)
                        artist.Metadata["genres"] = string.Join(", ", names);
                }

                if (attributes.TryGetProperty("artwork", out var artwork) && artwork.ValueKind == JsonValueKind.Object)
                    artist.ImageUrl = ToArtworkUrl(artwork, size: 300);
            }

            return artist;
        }

        private static TrackInfo ParseSong(JsonElement item)
        {
            var track = new TrackInfo();

            if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                track.Id = idEl.GetString() ?? string.Empty;

            if (TryGetAttributes(item, out var attributes))
            {
                if (attributes.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                    track.Name = nameEl.GetString() ?? string.Empty;

                if (attributes.TryGetProperty("artistName", out var artistEl) && artistEl.ValueKind == JsonValueKind.String)
                    track.ArtistName = artistEl.GetString() ?? string.Empty;

                if (attributes.TryGetProperty("albumName", out var albumEl) && albumEl.ValueKind == JsonValueKind.String)
                    track.AlbumName = albumEl.GetString() ?? string.Empty;

                if (attributes.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    track.ExternalUrl = urlEl.GetString();

                if (attributes.TryGetProperty("artwork", out var artwork) && artwork.ValueKind == JsonValueKind.Object)
                    track.ImageUrl = ToArtworkUrl(artwork, size: 300);

                if (attributes.TryGetProperty("previews", out var previews) &&
                    previews.ValueKind == JsonValueKind.Array &&
                    previews.GetArrayLength() > 0)
                {
                    var preview = previews[0];
                    if (preview.TryGetProperty("url", out var previewUrl) && previewUrl.ValueKind == JsonValueKind.String)
                        track.PreviewUrl = previewUrl.GetString();
                }
            }

            track.Metadata["source"] = "apple_music";
            return track;
        }

        private static bool TryGetAttributes(JsonElement item, out JsonElement attributes)
        {
            if (item.TryGetProperty("attributes", out attributes) && attributes.ValueKind == JsonValueKind.Object)
                return true;

            attributes = default;
            return false;
        }

        private static string? ToArtworkUrl(JsonElement artwork, int size)
        {
            if (!artwork.TryGetProperty("url", out var urlEl) || urlEl.ValueKind != JsonValueKind.String)
                return null;

            var template = urlEl.GetString();
            if (string.IsNullOrWhiteSpace(template))
                return null;

            // Example template: "https://.../{w}x{h}bb.jpg"
            return template
                .Replace("{w}", size.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{h}", size.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetAppleId(ArtistInfo artist, out string appleId)
        {
            if (artist.Metadata.TryGetValue("apple_music_id", out var id) && !string.IsNullOrWhiteSpace(id))
            {
                appleId = id!;
                return true;
            }

            appleId = string.Empty;
            return false;
        }
    }
}

