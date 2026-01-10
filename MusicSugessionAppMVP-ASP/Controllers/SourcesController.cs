using Microsoft.AspNetCore.Mvc;
using MusicSugessionAppMVP_ASP.Models;
using MusicSugessionAppMVP_ASP.Services;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class SourcesController : Controller
    {
        static ConcurrentDictionary<string, CrateSessionState> _crates = new();


        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            {
                return RedirectToAction(
                    "Login",
                    "Home",
                    new { returnUrl = HttpContext.Request.Path }
                );
            }

            return View();
        }

        [HttpGet]
        public IActionResult Review()
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToAction("Login", "Home");

            return View();
        }



        [HttpPost]
        public async Task<IActionResult> CreateCrate(CreateCrateInputModel input)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToAction("Login", "Home");

            var clientId = "dbb1ff804d89446f8c744d200b20e2d8";
            var clientSecret = "57681c65030c4ea49e563f2ca643d1b4";

            var spotify = new SpotifyService(clientId, clientSecret);
            var musicBrainzService = new MusicBrainzService();
            var deezer = new DeezerService();

            var resolvedArtists = new List<ArtistInfo>();

            foreach (var name in new[] { input.Artist1, input.Artist2, input.Artist3 })
            {
                var artist = await ResolveArtistAsync(spotify, musicBrainzService, name);
                if (artist == null)
                {
                    ModelState.AddModelError("", $"Artist not found: {name}");
                    return View("Index");
                }

                resolvedArtists.Add(artist);
            }


            var sessionId = HttpContext.Session.Id;

            var crate = new CrateSessionState
            {
                SelectedGenres = ExtractGenres(resolvedArtists)
            };

            _crates[sessionId] = crate;

            // 🔥 Fire-and-forget background fill (IMPORTANT)
            _ = Task.Run(() =>
                FillCrateAsync(sessionId, resolvedArtists, deezer, spotify, musicBrainzService)
            );

            // Redirect to review screen (empty initially)
            return RedirectToAction("Review");
        }

        private HashSet<string> ExtractGenres(List<ArtistInfo> resolvedArtists)
        {
            var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var artist in resolvedArtists)
            {
                if (artist.Metadata.TryGetValue("genres", out var g) && g != null)
                {
                    foreach (var genre in g.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        genres.Add(genre.Trim());
                }
            }

            return genres;
        }

        private async Task FillCrateAsync(
    string sessionId,
    List<ArtistInfo> seedArtists,
    DeezerService deezer,
    SpotifyService spotify,
    MusicBrainzService musicBrainz)
        {
            var crate = _crates[sessionId];
            var aggregator = new SimilarArtistAggregationService(deezer);

            foreach (var seed in seedArtists)
            {
                var related =
                    await aggregator.GetSimilarArtistsWithAtLeastOneTrackAsync(
                        seed.Name, 30);

                foreach (var candidate in related)
                {
                    if (!crate.SeenArtists.Add(candidate.Name))
                        continue;

                    await EnrichArtist(candidate, spotify);

                    if (!await ArtistValdation(crate.SelectedGenres, candidate,seedArtists, spotify,
                        deezer,musicBrainz))
                        continue;

                    var shuffled = candidate.Tracks
                        .OrderBy(_ => Random.Shared.Next())
                        .ToList();

                    if (shuffled.Count == 0)
                        continue;

                    lock (crate)
                    {
                        crate.PrimaryQueue.Enqueue(shuffled[0]);
                        foreach (var t in shuffled.Skip(1))
                            crate.BackupQueue.Add(t);

                        // equivalent of "open review window"
                        if (!crate.IsWarm && crate.PrimaryQueue.Count >= 3)
                            crate.IsWarm = true;
                    }

                    await Task.Delay(50);
                }
            }
        }

        private async Task<bool> ArtistValdation(HashSet<string> selectedGenres,
            ArtistInfo similarArtist, List<ArtistInfo> selectedArtists,
            SpotifyService spotifyService,
            DeezerService deezerService,
            MusicBrainzService musicBrainzService)
        {
            if (selectedArtists.Any(a =>
                string.Equals(a.Name, similarArtist.Name, StringComparison.OrdinalIgnoreCase)))
                return false;

            bool hasGenres = await PopulateGenresFromAllSourcesAsync(similarArtist, spotifyService,
                 deezerService, musicBrainzService);
            if (!hasGenres)
                return false;

            if (!similarArtist.Metadata.TryGetValue("genres", out var genreText))
                return false;

            var candidateGenres = genreText
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim());

            if (!candidateGenres.Any(g => selectedGenres.Contains(g)))
                return false;

            return true;
        }


        private async Task<bool> PopulateGenresFromAllSourcesAsync(ArtistInfo artist, SpotifyService spotifyService
            , DeezerService deezerService, MusicBrainzService musicBrainzService)
        {
            var genreSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // --- Spotify ---
            if (spotifyService != null && !string.IsNullOrWhiteSpace(artist.SpotifyId))
            {
                try
                {
                    var spotifyArtist = await spotifyService.GetArtistByIdAsync(artist.SpotifyId);
                    if (spotifyArtist?.Metadata.TryGetValue("genres", out var sg) == true &&
                        !string.IsNullOrWhiteSpace(sg))
                    {
                        foreach (var g in sg.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            genreSet.Add(g.Trim());
                    }
                }
                catch
                {
                    // swallow intentionally (same as original behavior)
                }
            }

            // --- MusicBrainz ---
            if (musicBrainzService != null)
            {
                var mbGenres = await musicBrainzService.GetGenresAsync(artist.Name);

                foreach (var g in mbGenres)
                    if (!string.IsNullOrWhiteSpace(g))
                        genreSet.Add(g.Trim());
            }

            if (genreSet.Count > 0)
            {
                artist.Metadata["genres"] = string.Join(", ", genreSet);
                return true;
            }

            return false;
        }

        private async Task EnrichArtist(ArtistInfo artist, SpotifyService spotifyService)
        {
            if (artist.SpotifyId == null && spotifyService != null)
            {
                var match = await spotifyService.GetArtistByNameAsync(artist.Name);
                if (match != null)
                {
                    artist.SpotifyId = match.SpotifyId;
                    artist.ImageUrl ??= match.ImageUrl;
                }
            }
        }

        [HttpGet]
        public IActionResult NextTrack()
        {
            var sessionId = HttpContext.Session.Id;

            if (!_crates.TryGetValue(sessionId, out var crate))
                return NoContent();

            lock (crate)
            {
                if (!crate.IsWarm || crate.PrimaryQueue.Count == 0)
                    return NoContent();

                var track = crate.PrimaryQueue.Dequeue();
                return Json(track);
            }
        }


        [HttpPost]
        public IActionResult LikeTrack([FromBody] TrackInfo track)
        {
            var sessionId = HttpContext.Session.Id;

            if (!_crates.TryGetValue(sessionId, out var crate))
                return Ok();

            //_ = Task.Run(() =>
            //    ExpandFromLikedTrackAsync(sessionId, track)
            //);

            return Ok();
        }


        private async Task<ArtistInfo?> ResolveArtistAsync(
    SpotifyService spotify,MusicBrainzService musicBrainzService,
    string inputName)
        {
            var results = await spotify.SearchArtistsAsync(inputName);

            if (results.Count == 0)
                return null;

            var exact =
                results.FirstOrDefault(a =>
                    string.Equals(a.Name, inputName,
                        StringComparison.OrdinalIgnoreCase));

            var selected = exact ?? results
                .OrderByDescending(a => SimilarityScore(inputName, a.Name))
                .First();

            var fullArtists = await GetFullSpotifyArtists(new List<ArtistInfo>() { selected }, spotify);
            var artistsWithGenres = await AddMusicBrainzGenretoArtists(fullArtists, musicBrainzService);

            return artistsWithGenres.FirstOrDefault();
        }


        private async Task<IEnumerable<ArtistInfo>> GetFullSpotifyArtists(IEnumerable<ArtistInfo> artists, SpotifyService spotifyService)
        {
            var list = new List<ArtistInfo>();

            foreach (var artist in artists)
            {
                if (spotifyService != null && !string.IsNullOrWhiteSpace(artist.SpotifyId))
                {
                    var full = await spotifyService.GetArtistByIdAsync(artist.SpotifyId);
                    if (full != null)
                        list.Add(full);
                }
            }

            return list;
        }

        private async Task<IEnumerable<ArtistInfo>> AddMusicBrainzGenretoArtists(IEnumerable<ArtistInfo> artists, MusicBrainzService musicBrainzService)
        {
            foreach (var artist in artists)
            {
                bool hasGenres =
                    artist.Metadata.TryGetValue("genres", out var g) &&
                    !string.IsNullOrWhiteSpace(g);

                if (!hasGenres && musicBrainzService != null)
                {
                    var mbGenres = await musicBrainzService.GetGenresAsync(artist.Name);
                    if (mbGenres.Count > 0)
                        artist.Metadata["genres"] = string.Join(", ", mbGenres);
                }
            }

            return artists;
        }

        private static int SimilarityScore(string a, string b)
        {
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();

            if (a == b) return 100;
            if (b.Contains(a) || a.Contains(b)) return 80;

            return 0;
        }


    }
}
