using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicSugessionAppMVP_ASP.Models;
using MusicSugessionAppMVP_ASP.Persistance;
using MusicSugessionAppMVP_ASP.Security;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class ProfileController : Controller
    {

        private readonly Persistence _db;

        public ProfileController(Persistence db)
        {
            _db = db;
        }

        public IActionResult Index()
            {
                var email = HttpContext.Session.GetString("Email");
                if (string.IsNullOrEmpty(email))
                    return RedirectToAction("CreateProfile", "Register");

                var user = _db.Users
                    .Include(u => u.PreferredStreamingPlatform)
                    .FirstOrDefault(u => u.Email == email);

                if (user == null)
                    return RedirectToAction("CreateProfile", "Register");

                var vm = new ProfileViewModel
                {
                    Name = user.Name,
                    Email = user.Email,
                    PreferredStreamingPlatformId = user.PreferredStreamingPlatformId,
                    StreamingPlatforms = _db.StreamingPlatforms
                        .Select(sp => new StreamingPlatformOption
                        {
                            Id = sp.Id,
                            Name = sp.Name
                        }).ToList()
                };

                return View(vm);
            }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(ProfileViewModel model)
        {
            var currentEmail = HttpContext.Session.GetString("Email");
            if (string.IsNullOrEmpty(currentEmail))
                return RedirectToAction("CreateProfile", "Register");

            var user = _db.Users.FirstOrDefault(u => u.Email == currentEmail);
            if (user == null)
                return RedirectToAction("CreateProfile", "Register");

            // Email changed?
            if (model.Email != currentEmail)
            {
                if (_db.Users.Any(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email already in use");
                }
            }

            if (!ModelState.IsValid)
            {
                model.StreamingPlatforms = _db.StreamingPlatforms
                    .Select(sp => new StreamingPlatformOption
                    {
                        Id = sp.Id,
                        Name = sp.Name
                    }).ToList();
                return View(model);
            }

            // Apply updates
            user.Name = model.Name;
            user.Email = model.Email;
            user.PreferredStreamingPlatformId = model.PreferredStreamingPlatformId;

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                user.PasswordHash = PasswordHasher.Hash(model.NewPassword);
            }

            _db.SaveChanges();

            // 🔴 CRITICAL: Update session email if changed
            if (model.Email != currentEmail)
            {
                HttpContext.Session.SetString("Email", model.Email);
            }

            ViewBag.Success = true;
            return RedirectToAction("Index","Home");
        }

    }
}
