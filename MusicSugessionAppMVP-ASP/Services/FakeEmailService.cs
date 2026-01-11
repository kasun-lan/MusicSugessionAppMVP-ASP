using MusicSugessionAppMVP_ASP.Models;

namespace MusicSugessionAppMVP_ASP.Services
{
    public class FakeEmailService
    {
        public Task SendPlaylistAsync(
            string email,
            IEnumerable<TrackInfo> tracks)
        {
            // 🔴 SIMULATION ONLY
            Console.WriteLine("================================");
            Console.WriteLine($"Simulated email sent to: {email}");
            Console.WriteLine("Tracks:");

            foreach (var t in tracks)
            {
                Console.WriteLine($"- {t.ArtistName} — {t.Name}");
            }

            Console.WriteLine("================================");

            return Task.CompletedTask;
        }
    }
}
