using MusicSugessionAppMVP_ASP.Models;
using System.IO.Pipes;

namespace MusicSugessionAppMVP_ASP.Persistance
{

    public class Session
    {
        public Guid Id { get; set; }

        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }

        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public DeviceType DeviceType { get; set; } // Mobile / Desktop

        public ICollection<SessionInputArtist> InputArtists { get; set; }
        public ICollection<SwipeEvent> SwipeEvents { get; set; }
    }

    public class SessionExport
    {
        public Guid Id { get; set; }

        public Guid SessionId { get; set; }
        public Session Session { get; set; }

        public ExportMedium Medium { get; set; } // Email, Spotify, AppleMusic, etc.

        public DateTime ExportedAtUtc { get; set; }
    }

    public class TrackExposure
    {
        public Guid Id { get; set; }

        public Guid SessionId { get; set; }
        public Session Session { get; set; }

        public Guid TrackId { get; set; }
        public Track Track { get; set; }

        public DateTime ExposedAtUtc { get; set; }
    }

    public class SessionStatsSnapshot
    {
        public Guid SessionId { get; set; }

        public int TotalSwipes { get; set; }
        public int Likes { get; set; }
        public int Dislikes { get; set; }

        public DateTime CapturedAtUtc { get; set; }
    }

    public class ArtistExposure
    {
        public Guid Id { get; set; }

        public Guid SessionId { get; set; }
        public Session Session { get; set; }

        public Guid ArtistId { get; set; }
        public Artist Artist { get; set; }

        public DateTime ExposedAtUtc { get; set; }
    }

    public class GenreExposure
    {
        public Guid Id { get; set; }

        public Guid SessionId { get; set; }
        public Session Session { get; set; }

        public Guid GenreId { get; set; }
        public Genre Genre { get; set; }

        public DateTime ExposedAtUtc { get; set; }
    }






    public class User
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }

        public Guid PreferredStreamingPlatformId { get; set; }
        public StreamingPlatform PreferredStreamingPlatform { get; set; }

        public DateTime RegisteredAtUtc { get; set; }

        public ICollection<Session> Sessions { get; set; }
        public ICollection<UserRoleAssignment> Roles { get; set; }
    }

    public enum UserRole
    {
        DJ = 1,
        Musician = 2,
        Producer = 3,
        ProfessionalCurator = 4
    }

    public class UserRoleAssignment
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }

        public UserRole Role { get; set; }
    }



    public class SessionInputArtist
    {
        public Guid Id { get; set; }

        public Guid SessionId { get; set; }
        public Session Session { get; set; }

        public Guid ArtistId { get; set; }
        public Artist Artist { get; set; }

        public int Position { get; set; } // 1, 2, 3
    }

    public class Artist
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string? SpotifyId { get; set; }
        public string? DeezerId { get; set; }
        public string? MusicBrainzId { get; set; }

        public ICollection<Track> Tracks { get; set; }
    }


    public class Track
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public Guid ArtistId { get; set; }
        public Artist Artist { get; set; }

        public ICollection<TrackGenre> Genres { get; set; }
    }

    public class Genre
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public ICollection<TrackGenre> Tracks { get; set; }
    }

    public class TrackGenre
    {
        public Guid TrackId { get; set; }
        public Track Track { get; set; }

        public Guid GenreId { get; set; }
        public Genre Genre { get; set; }
    }

    public class SwipeEvent
    {
        public Guid Id { get; set; }

        public Guid SessionId { get; set; }
        public Session Session { get; set; }

        public Guid TrackId { get; set; }
        public Track Track { get; set; }

        public SwipeDirection Direction { get; set; } // Like / Dislike

        public DateTime SwipedAtUtc { get; set; }
    }

    public enum SwipeDirection
    {
        Like = 1,
        Dislike = 2
    }

    public class ArtistSimilarityApi
    {
        public Guid Id { get; set; }

        public Guid SourceArtistId { get; set; }
        public Guid SimilarArtistId { get; set; }

        public string Provider { get; set; } // Deezer, Spotify
    }

    public class ArtistSimilarityInternal
    {
        public Guid Id { get; set; }

        public Guid SourceArtistId { get; set; }
        public Guid SimilarArtistId { get; set; }

        public string Reason { get; set; } // genre overlap, user behavior, etc.
    }

    public class ArtistNoSimilarFound
    {
        public Guid Id { get; set; }

        public Guid ArtistId { get; set; }
        public Artist Artist { get; set; }

        public string Source { get; set; } // Deezer, Spotify, Internal

        public DateTime RecordedAtUtc { get; set; }
    }

    public class StreamingPlatform
    {
        public Guid Id { get; set; }

        public string Name { get; set; } // Spotify, Apple Music, Deezer
    }

    public enum DeviceType
    {
        Desktop = 1,
        Mobile = 2
    }

    public class SessionStats
    {
        public Guid SessionId { get; set; }

        public int TotalSwipes { get; set; }
        public int Likes { get; set; }
        public int Dislikes { get; set; }
    }

    public class TrackStats
    {
        public Guid TrackId { get; set; }

        public int TotalLikes { get; set; }
        public int TotalDislikes { get; set; }
    }

}
