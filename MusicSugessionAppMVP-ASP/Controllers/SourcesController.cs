using Microsoft.AspNetCore.Mvc;
using MusicSugessionAppMVP_ASP.Models;
using MusicSugessionAppMVP_ASP.Services;
using System.Text.Json;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class SourcesController : Controller
    {
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

        [HttpPost]
        public async Task<IActionResult> CreateCrate(CreateCrateInputModel input)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToAction("Login", "Home");

            var clientId = "dbb1ff804d89446f8c744d200b20e2d8";
            var clientSecret = "57681c65030c4ea49e563f2ca643d1b4";

            var spotify = new SpotifyService(clientId, clientSecret);
            var musicBrainzService = new MusicBrainzService();

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

            HttpContext.Session.SetString(
                "CrateArtists",
                JsonSerializer.Serialize(resolvedArtists)
            );

            return RedirectToAction("Review"); // next step
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
