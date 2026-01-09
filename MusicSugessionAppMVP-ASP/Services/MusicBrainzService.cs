using MusicSugessionAppMVP_ASP.Models;
using System.Net;
using System.Text.Json;

namespace MusicSugessionAppMVP_ASP.Services
{
    public class MusicBrainzService
    {
        private readonly HttpClient _client;

        // ============================
        // Rate limiting (1 req / sec)
        // ============================
        private readonly SemaphoreSlim _rateLock = new(1, 1);
        private DateTime _lastRequestUtc = DateTime.MinValue;
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

        private static readonly HashSet<string> KnownGenres = new(StringComparer.OrdinalIgnoreCase)
{
    // Core / Existing
    "hip hop", "hip-hop", "rap", "pop rap", "trap", "boom bap",
    "rock", "pop", "metal", "jazz", "soul", "blues", "electronic",
    "indie", "alternative", "classical", "r&b", "punk", "house",
    "techno", "folk", "country",

    // Commercial / Media
    "commercial",
    "jingles",
    "tv themes",
    "soundtrack",
    "movie soundtrack",
    "tv soundtrack",
    "original score",
    "musicals",
    "spoken word",
    "karaoke",

    // Country & Related
    "alternative country",
    "americana",
    "australian country",
    "bakersfield sound",
    "bluegrass",
    "progressive bluegrass",
    "reactionary bluegrass",
    "blues country",
    "cajun fiddle tunes",
    "christian country",
    "classic country",
    "close harmony",
    "contemporary bluegrass",
    "contemporary country",
    "country gospel",
    "country pop",
    "country rap",
    "country rock",
    "country soul",
    "cowboy",
    "western",
    "cowpunk",
    "dansband",
    "honky tonk",
    "franco-country",
    "gulf and western",
    "hellbilly music",
    "instrumental country",
    "lubbock sound",
    "nashville sound",
    "neotraditional country",
    "outlaw country",
    "progressive country",
    "psychobilly",
    "punkabilly",
    "red dirt",
    "sertanejo",
    "texas country",
    "traditional bluegrass",
    "traditional country",
    "truck-driving country",
    "urban cowboy",
    "western swing",
    "zydeco",

    // EDM / Dance
    "dance",
    "club",
    "club dance",
    "breakcore",
    "breakbeat",
    "breakstep",
    "4-beat",
    "acid breaks",
    "baltimore club",
    "big beat",
    "breakbeat hardcore",
    "broken beat",
    "florida breaks",
    "nu skool breaks",
    "brostep",
    "chillstep",
    "deep house",
    "dubstep",
    "electro house",
    "electroswing",
    "future garage",
    "garage",
    "glitch hop",
    "glitch pop",
    "grime",
    "hardcore",
    "bouncy house",
    "bouncy techno",
    "digital hardcore",
    "doomcore",
    "dubstyle",
    "gabber",
    "happy hardcore",
    "hardstyle",
    "jumpstyle",
    "makina",
    "speedcore",
    "terrorcore",
    "uk hardcore",
    "hard dance",
    "hi-nrg",
    "eurodance",
    "house",
    "acid house",
    "chicago house",
    "diva house",
    "dutch house",
    "freestyle house",
    "french house",
    "funky house",
    "ghetto house",
    "hip house",
    "italo house",
    "latin house",
    "minimal house",
    "progressive house",
    "swing house",
    "tech house",
    "tribal house",
    "tropical house",
    "vocal house",
    "jackin house",

    // Techno / Trance
    "jungle",
    "drum and bass",
    "liquid dub",
    "acid techno",
    "detroit techno",
    "minimal techno",
    "free tekno",
    "trance",
    "acid trance",
    "classic trance",
    "goa trance",
    "psytrance",
    "dark psytrance",
    "full on",
    "psybreaks",
    "psyprog",
    "suomisaundi",
    "uplifting trance",
    "vocal trance",

    // Electronic (Non-dance)
    "ambient",
    "ambient dub",
    "ambient house",
    "dark ambient",
    "drone",
    "illbient",
    "lowercase",
    "chillwave",
    "chiptune",
    "bitpop",
    "nintendocore",
    "video game music",
    "downtempo",
    "acid jazz",
    "trip hop",
    "idm",
    "industrial",
    "vaporwave",
    "mallsoft",
    "uk garage",

    // Hip-Hop Subgenres
    "alternative rap",
    "avant-garde rap",
    "bounce",
    "chap hop",
    "christian hip hop",
    "conscious hip hop",
    "dirty south",
    "east coast hip hop",
    "hardcore hip hop",
    "gangsta rap",
    "golden age hip hop",
    "instrumental hip hop",
    "jazz rap",
    "latin rap",
    "nerdcore",
    "new school hip hop",
    "old school rap",
    "underground rap",
    "west coast rap",

    // Jazz
    "bebop",
    "big band",
    "cool jazz",
    "fusion",
    "gypsy jazz",
    "hard bop",
    "latin jazz",
    "modal jazz",
    "smooth jazz",
    "swing jazz",
    "trad jazz",

    // Metal
    "heavy metal",
    "thrash metal",
    "death metal",
    "black metal",
    "doom metal",
    "power metal",
    "folk metal",
    "symphonic metal",
    "industrial metal",
    "progressive metal",
    "metalcore",
    "deathcore",
    "nu metal",
    "djent",

    // Pop Variants
    "dance pop",
    "dream pop",
    "electropop",
    "synthpop",
    "teen pop",
    "traditional pop",

    // World / Regional
    "afrobeat",
    "highlife",
    "mbalax",
    "kizomba",
    "kwaito",
    "rai",
    "soca",
    "calypso",
    "reggaeton",
    "samba",
    "bossa nova",
    "flamenco",
    "tango",
    "fado",
    "k-pop",
    "j-pop",
    "mandopop",
    "c-pop",
    "hindustani",
    "carnatic",
    "filmi",
    "bhangra",
    "qawwali",

    // Vocal / Instrumental
    "a cappella",
    "barbershop",
    "gregorian chant",
    "vocal jazz",
    "instrumental",

    // New Age / Wellness
    "new age",
    "meditation",
    "nature",
    "relaxation",
    "healing"
};


        public MusicBrainzService(HttpClient? httpClient = null)
        {
            // 🔴 CRITICAL: Force TLS 1.2 (fixes EOF / SSL failures on Windows)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            _client = httpClient ?? new HttpClient();

            // 🔴 REQUIRED by MusicBrainz
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MusicDiscoveryAppPOC/1.0 (contact: nadeeshancooray@gmail.com)");

            _client.DefaultRequestVersion = HttpVersion.Version11;
            _client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;


            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        // ============================
        // Rate-limited HTTP helper
        // ============================
        private async Task<string> GetStringRateLimitedAsync(string url)
        {
            await _rateLock.WaitAsync();
            try
            {
                var elapsed = DateTime.UtcNow - _lastRequestUtc;
                if (elapsed < MinInterval)
                    await Task.Delay(MinInterval - elapsed);

                _lastRequestUtc = DateTime.UtcNow;

                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        using var response = await _client.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                    catch (HttpRequestException) when (attempt == 1)
                    {
                        await Task.Delay(500); // short backoff
                    }
                }

                throw new HttpRequestException("MusicBrainz request failed after retry.");
            }
            finally
            {
                _rateLock.Release();
            }
        }


        // ============================
        // Public API
        // ============================
        public async Task<List<string>> GetGenresAsync(string artistName)
        {
            var mbid = await SearchArtistIdAsync(artistName);
            if (mbid == null)
                return new List<string>();

            return await GetGenresByIdAsync(mbid);
        }

        // ============================
        // Artist lookup
        // ============================
        private async Task<string?> SearchArtistIdAsync(string artistName)
        {
            var url =
                $"https://musicbrainz.org/ws/2/artist/" +
                $"?query={Uri.EscapeDataString(artistName)}&limit=1&fmt=json";

            var json = await GetStringRateLimitedAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("artists", out var artists) ||
                artists.GetArrayLength() == 0)
                return null;

            return artists[0].GetProperty("id").GetString();
        }

        // ============================
        // Genre extraction
        // ============================
        private async Task<List<string>> GetGenresByIdAsync(string mbid)
        {
            var url =
                $"https://musicbrainz.org/ws/2/artist/{mbid}" +
                $"?inc=tags+genres&fmt=json";

            var json = await GetStringRateLimitedAsync(url);
            using var doc = JsonDocument.Parse(json);

            var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Official genres
            if (doc.RootElement.TryGetProperty("genres", out var genreArray))
            {
                foreach (var g in genreArray.EnumerateArray())
                {
                    var name = g.GetProperty("name").GetString();
                    if (!string.IsNullOrWhiteSpace(name) &&
                        KnownGenres.Contains(name))
                    {
                        genres.Add(name);
                    }
                }
            }

            // Fallback to tags
            if (genres.Count == 0 &&
                doc.RootElement.TryGetProperty("tags", out var tagArray))
            {
                foreach (var t in tagArray.EnumerateArray())
                {
                    var name = t.GetProperty("name").GetString();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (KnownGenres.Any(kg =>
                        name.Contains(kg, StringComparison.OrdinalIgnoreCase)))
                    {
                        genres.Add(name);
                    }
                }
            }

            return genres.ToList();
        }

        // ============================
        // Optional: recordings / tracks
        // ============================
        public async Task<List<TrackInfo>> GetMusicBrainzArtistTracksAsync(
            string artistMbid,
            int limit = 3,
            CancellationToken cancellationToken = default)
        {
            var url =
                $"https://musicbrainz.org/ws/2/recording" +
                $"?artist={artistMbid}&limit={limit}&fmt=json";

            await _rateLock.WaitAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var elapsed = now - _lastRequestUtc;
                if (elapsed < MinInterval)
                    await Task.Delay(MinInterval - elapsed, cancellationToken);

                _lastRequestUtc = DateTime.UtcNow;

                using var response = await _client.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream =
                    await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json =
                    await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                var tracks = new List<TrackInfo>();

                if (!json.RootElement.TryGetProperty("recordings", out var recordings))
                    return tracks;

                foreach (var item in recordings.EnumerateArray())
                    tracks.Add(ParseRecording(item));

                return tracks;
            }
            finally
            {
                _rateLock.Release();
            }
        }

        private TrackInfo ParseRecording(JsonElement item)
        {
            var track = new TrackInfo
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Name = item.GetProperty("title").GetString() ?? string.Empty,
                Popularity = 0,
                PreviewUrl = null,
                ImageUrl = null,
                ExternalUrl =
                    $"https://musicbrainz.org/recording/{item.GetProperty("id").GetString()}"
            };

            if (item.TryGetProperty("artist-credit", out var artistCredit) &&
                artistCredit.GetArrayLength() > 0)
            {
                track.ArtistName =
                    artistCredit[0].GetProperty("name").GetString() ?? string.Empty;
            }

            if (item.TryGetProperty("releases", out var releases) &&
                releases.GetArrayLength() > 0)
            {
                track.AlbumName =
                    releases[0].GetProperty("title").GetString() ?? string.Empty;
            }

            track.Metadata["source"] = "MusicBrainz";
            return track;
        }
    }
}

