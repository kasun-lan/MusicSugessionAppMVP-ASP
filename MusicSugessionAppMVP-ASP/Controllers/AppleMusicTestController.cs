using Microsoft.AspNetCore.Mvc;
using MusicSugessionAppMVP_ASP.Services;
using System.Net.Http.Headers;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    [Route("apple-music-test")]
    public class AppleMusicTestController : Controller
    {
        private readonly AppleMusicTokenService _tokenService;
        private readonly IHttpClientFactory _httpClientFactory;

        public AppleMusicTestController(
            AppleMusicTokenService tokenService,
            IHttpClientFactory httpClientFactory)
        {
            _tokenService = tokenService;
            _httpClientFactory = httpClientFactory;
        }

        // ======= VIEW FOR TESTING =======
        public IActionResult Index()
        {
            return View();
        }

        // ======= ENDPOINT: get developer token for browser =======
        [HttpGet("developer-token")]
        public IActionResult GetDeveloperToken()
        {
            var token = _tokenService.GenerateDeveloperToken();
            return Ok(new { developerToken = token });
        }

        // ======= ENDPOINT: receive and store user token =======
        // (For testing we just keep it in memory - replace with DB later)
        private static string _lastUserToken;

        [HttpPost("store-user-token")]
        public IActionResult StoreUserToken([FromBody] UserTokenDto dto)
        {
            _lastUserToken = dto.Token;
            return Ok(new { message = "User token stored" });
        }

        // ======= ENDPOINT: CREATE PLAYLIST FOR USER =======
        [HttpPost("create-playlist")]
        public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistDto dto)
        {
            if (string.IsNullOrEmpty(_lastUserToken))
                return BadRequest("No user token stored. Authorize first.");

            var developerToken = _tokenService.GenerateDeveloperToken();

            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", developerToken);

            client.DefaultRequestHeaders.Add("Music-User-Token", _lastUserToken);

            var body = new
            {
                attributes = new
                {
                    name = dto.Name,
                    description = "Created from ASP.NET test controller"
                }
            };

            var response = await client.PostAsJsonAsync(
                "https://api.music.apple.com/v1/me/library/playlists",
                body);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, json);

            return Ok(json);
        }

        public class UserTokenDto
        {
            public string Token { get; set; }
        }

        public class CreatePlaylistDto
        {
            public string Name { get; set; }
        }
    }
}
