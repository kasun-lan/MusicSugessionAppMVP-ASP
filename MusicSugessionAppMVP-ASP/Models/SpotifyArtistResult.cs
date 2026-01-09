namespace MusicSugessionAppMVP_ASP.Models
{
    public class SpotifyArtistResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> Genres { get; set; } = new();
    }
}
