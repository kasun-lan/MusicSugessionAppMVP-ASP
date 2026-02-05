using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class SoundCloudController : Controller
    {
        private const string ClientId = "YOUR_CLIENT_ID";
        private const string ClientSecret = "YOUR_CLIENT_SECRET";
        private const string RedirectUri = "https://localhost:5001/soundcloud/callback";

        public IActionResult Login()
        {
            var authUrl =
                $"https://soundcloud.com/connect?" +
                $"client_id={ClientId}" +
                $"&redirect_uri={RedirectUri}" +
                $"&response_type=code" +
                $"&scope=non-expiring";

            return Redirect(authUrl);
        }

        public async Task<IActionResult> Callback(string code)
        {
            using var client = new HttpClient();

            var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("client_secret", ClientSecret),
            new KeyValuePair<string, string>("redirect_uri", RedirectUri),
            new KeyValuePair<string, string>("code", code)
        });

            var response = await client.PostAsync(
                "https://api.soundcloud.com/oauth2/token",
                content);

            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var accessToken = json.RootElement.GetProperty("access_token").GetString();

            HttpContext.Session.SetString("SC_Token", accessToken);

            return RedirectToAction("CreatePlaylist");
        }

        public IActionResult CreatePlaylist()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePlaylist(string title)
        {
            var token = HttpContext.Session.GetString("SC_Token");
            if (token == null)
                return RedirectToAction("Login");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("OAuth", token);

            var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("playlist[title]", title)
        });

            var response = await client.PostAsync(
                "https://api.soundcloud.com/playlists",
                content);

            ViewBag.Result = await response.Content.ReadAsStringAsync();
            return View();
        }
    }
}
