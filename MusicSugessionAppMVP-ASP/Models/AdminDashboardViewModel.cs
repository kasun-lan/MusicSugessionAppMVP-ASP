namespace MusicSugessionAppMVP_ASP.Models
{
    public class AdminDashboardViewModel
    {
        // Charts
        public List<ChartPoint> SessionsPerDay { get; set; }
        public List<ChartPoint> SwipesPerDay { get; set; }
        public List<ChartPoint> RegistrationsPerDay { get; set; }

        // Pie charts
        public List<ChartPoint> PreferredStreamingPlatforms { get; set; }
        public List<ChartPoint> DeviceTypeDistribution { get; set; }

        // Lists / Numbers
        public List<string> MostPopularGenres { get; set; }
        public int TotalSwipes { get; set; }
        public double AverageSwipesPerSession { get; set; }
        public List<TrackSwipeDto> MostRightSwipedTracks { get; set; }
    }

    public class ChartPoint
    {
        public string Label { get; set; }
        public int Value { get; set; }
    }

    public class TrackSwipeDto
    {
        public string TrackName { get; set; }
        public string ArtistName { get; set; }
        public int Likes { get; set; }
    }
}
