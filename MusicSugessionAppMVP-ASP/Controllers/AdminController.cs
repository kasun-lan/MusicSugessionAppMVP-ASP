using Microsoft.AspNetCore.Mvc;
using MusicSugessionAppMVP_ASP.Models;
using MusicSugessionAppMVP_ASP.Persistance;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class AdminController : Controller
    {
        private readonly Persistence _db;

        public AdminController(Persistence db)
        {
            _db = db;
        }

        public IActionResult Dashboard()
        {
            var vm = new AdminDashboardViewModel
            {
                SessionsPerDay = _db.Sessions
    .GroupBy(s => s.StartedAtUtc.Date)
    .Select(g => new
    {
        Date = g.Key,
        Count = g.Count()
    })
    .OrderBy(x => x.Date)
    .AsEnumerable() // SQL ends here
    .Select(x => new ChartPoint
    {
        Label = x.Date.ToString("yyyy-MM-dd"),
        Value = x.Count
    })
    .ToList(),


                SwipesPerDay = _db.SwipeEvents
    .GroupBy(s => s.SwipedAtUtc.Date)
    .Select(g => new
    {
        Date = g.Key,
        Count = g.Count()
    })
    .OrderBy(x => x.Date)
    .AsEnumerable()
    .Select(x => new ChartPoint
    {
        Label = x.Date.ToString("yyyy-MM-dd"),
        Value = x.Count
    })
    .ToList(),
                RegistrationsPerDay = _db.Users
    .GroupBy(u => u.RegisteredAtUtc.Date)
    .Select(g => new
    {
        Date = g.Key,
        Count = g.Count()
    })
    .OrderBy(x => x.Date)
    .AsEnumerable()
    .Select(x => new ChartPoint
    {
        Label = x.Date.ToString("yyyy-MM-dd"),
        Value = x.Count
    })
    .ToList(),


                PreferredStreamingPlatforms = _db.Users
                    .GroupBy(u => u.PreferredStreamingPlatform.Name)
                    .Select(g => new ChartPoint
                    {
                        Label = g.Key,
                        Value = g.Count()
                    })
                    .ToList(),

                DeviceTypeDistribution = _db.Sessions
                    .GroupBy(s => s.DeviceType)
                    .Select(g => new ChartPoint
                    {
                        Label = g.Key.ToString(),
                        Value = g.Count()
                    })
                    .ToList(),

                MostPopularGenres = _db.GenreExposures
                    .GroupBy(g => g.Genre.Name)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList(),

                TotalSwipes = _db.SwipeEvents.Count(),

                AverageSwipesPerSession = _db.SwipeEvents.Count() /
                    (double)Math.Max(1, _db.Sessions.Count()),

                MostRightSwipedTracks = _db.SwipeEvents
    .Where(s => s.Direction == SwipeDirection.Like)
    .GroupBy(s => new
    {
        TrackName = s.Track.Name,
        ArtistName = s.Track.Artist.Name
    })
    .OrderByDescending(g => g.Count())
    .Take(10)
    .Select(g => new TrackSwipeDto
    {
        TrackName = g.Key.TrackName,
        ArtistName = g.Key.ArtistName,
        Likes = g.Count()
    })
    .ToList()

            };

            return View(vm);
        }
    }
}
