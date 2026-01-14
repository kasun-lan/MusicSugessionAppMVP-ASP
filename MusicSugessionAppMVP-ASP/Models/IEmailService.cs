namespace MusicSugessionAppMVP_ASP.Models
{
    public interface IEmailService
    {
        Task SendPlaylistAsync(string email, IEnumerable<TrackInfo> tracks);
    }
}
