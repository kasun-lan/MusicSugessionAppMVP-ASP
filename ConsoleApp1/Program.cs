using System.Text.Json;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var service = new SoundCloudService(new HttpClient());

            long? trackId = service.FindTrackIdAsync("Blinding Lights The Weeknd").Result.Value;

            if (trackId != null)
                Console.WriteLine($"Track ID: {trackId}");
            else
                Console.WriteLine("Track not found.");

        }
    }

    public class SoundCloudService
    {
        private readonly HttpClient _http;

        private const string ClientId = "f1nVmmTB4nQd6VoSRaLcxu0ydxaWtXVo";

        public SoundCloudService(HttpClient http)
        {
            _http = http;
        }

        public async Task<long?> FindTrackIdAsync(string trackName)
        {
            var url =
                $"https://api.soundcloud.com/tracks?q={Uri.EscapeDataString(trackName)}&client_id={ClientId}&limit=1";

            var response = await _http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.GetArrayLength() == 0)
                return null; // No results found

            var firstTrack = root[0];

            if (firstTrack.TryGetProperty("id", out var idProp))
                return idProp.GetInt64();

            return null;
        }
    }
}
