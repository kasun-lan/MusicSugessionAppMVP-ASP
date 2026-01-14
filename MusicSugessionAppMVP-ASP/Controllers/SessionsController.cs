using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicSugessionAppMVP_ASP.Persistance;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class SessionsController : Controller
    {
        private readonly Persistence _db;

        public SessionsController(Persistence db)
        {
            _db = db;
        }

        // GET: /Sessions
        public IActionResult Index()
        {
            // auth guard
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToAction("Login", "Home");

            var userEmail = HttpContext.Session.GetString("Email");

            var user = _db.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Email == userEmail);

            if (user == null)
                return RedirectToAction("Login", "Home");

            var sessions = _db.Sessions
                .Where(s => s.UserId == user.Id)
                .Include(s => s.InputArtists)
                    .ThenInclude(x => x.Artist)
                .OrderByDescending(s => s.StartedAtUtc)
                .AsNoTracking()
                .ToList();

            return View(sessions);
        }

        // GET: /Sessions/Details/{id}
        public IActionResult Details(Guid id)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") != "true")
                return RedirectToAction("Login", "Home");

            var userEmail = HttpContext.Session.GetString("Email");

            var session = _db.Sessions
                .Include(s => s.InputArtists)
                    .ThenInclude(x => x.Artist)
                .Include(s => s.SwipeEvents)
                    .ThenInclude(x => x.Track)
                        .ThenInclude(t => t.Artist)
                .Include(s => s.SessionExports)
                .FirstOrDefault(s => s.Id == id);

            if (session == null)
                return NotFound();

            // security: ensure ownership
            var user = _db.Users.FirstOrDefault(u => u.Email == userEmail);
            if (session.UserId != user?.Id)
                return Forbid();

            return View(session);
        }
    }
}
