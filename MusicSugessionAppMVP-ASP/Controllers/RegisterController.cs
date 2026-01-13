using Microsoft.AspNetCore.Mvc;
using MusicSugessionAppMVP_ASP.Models;
using MusicSugessionAppMVP_ASP.Persistance;
using System.Security.Cryptography;
using System.Text;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class RegisterController : Controller
    {
        private readonly ILogger<RegisterController> _logger;
        private readonly Persistence _db;

        public RegisterController(ILogger<RegisterController> logger, Persistence db)
        {
            _logger = logger;
            _db = db;
        }

        [HttpGet]
        public IActionResult CreateProfile()
        {
            var viewModel = new CreateProfileViewModel
            {
                StreamingPlatforms = _db.StreamingPlatforms
                    .Select(sp => new StreamingPlatformOption
                    {
                        Id = sp.Id,
                        Name = sp.Name
                    })
                    .ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateProfile(CreateProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.StreamingPlatforms = _db.StreamingPlatforms
                    .Select(sp => new StreamingPlatformOption
                    {
                        Id = sp.Id,
                        Name = sp.Name
                    })
                    .ToList();
                return View(model);
            }

            // Check if email already exists
            if (_db.Users.Any(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email already registered");
                model.StreamingPlatforms = _db.StreamingPlatforms
                    .Select(sp => new StreamingPlatformOption
                    {
                        Id = sp.Id,
                        Name = sp.Name
                    })
                    .ToList();
                return View(model);
            }

            // Check if name already exists
            if (_db.Users.Any(u => u.Name == model.Name))
            {
                ModelState.AddModelError("Name", "Username already taken");
                model.StreamingPlatforms = _db.StreamingPlatforms
                    .Select(sp => new StreamingPlatformOption
                    {
                        Id = sp.Id,
                        Name = sp.Name
                    })
                    .ToList();
                return View(model);
            }

            // Get streaming platform from database
            var streamingPlatform = _db.StreamingPlatforms
                .FirstOrDefault(sp => sp.Id == model.PreferredStreamingPlatformId);

            if (streamingPlatform == null)
            {
                ModelState.AddModelError("PreferredStreamingPlatformId", "Invalid streaming platform selected");
                model.StreamingPlatforms = _db.StreamingPlatforms
                    .Select(sp => new StreamingPlatformOption
                    {
                        Id = sp.Id,
                        Name = sp.Name
                    })
                    .ToList();
                return View(model);
            }

            // Hash password
            var passwordHash = HashPassword(model.Password);

            // Create user
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = model.Name,
                Email = model.Email,
                PasswordHash = passwordHash,
                PreferredStreamingPlatformId = streamingPlatform.Id,
                RegisteredAtUtc = DateTime.UtcNow,
                Sessions = new List<Session>(),
                Roles = new List<UserRoleAssignment>()
            };

            _db.Users.Add(user);
            _db.SaveChanges();

            // Store user ID in TempData for role selection
            TempData["UserId"] = user.Id.ToString();
            TempData["UserName"] = user.Name;

            return RedirectToAction("CreateProfileRole");
        }

        [HttpGet]
        public IActionResult CreateProfileRole()
        {
            if (TempData["UserId"] == null)
            {
                return RedirectToAction("CreateProfile");
            }

            var userId = Guid.Parse(TempData["UserId"].ToString());
            var viewModel = new CreateProfileRoleViewModel
            {
                UserId = userId
            };

            // Preserve TempData for POST
            TempData.Keep("UserId");
            TempData.Keep("UserName");

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateProfileRole(CreateProfileRoleViewModel model)
        {
            if (TempData["UserId"] == null)
            {
                return RedirectToAction("CreateProfile");
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == model.UserId);
            if (user == null)
            {
                return RedirectToAction("CreateProfile");
            }

            // Add selected roles
            var rolesToAdd = new List<UserRole>();

            if (model.IsDJ)
                rolesToAdd.Add(UserRole.DJ);
            if (model.IsMusician)
                rolesToAdd.Add(UserRole.Musician);
            if (model.IsProducer)
                rolesToAdd.Add(UserRole.Producer);
            if (model.IsProfessionalCurator)
                rolesToAdd.Add(UserRole.ProfessionalCurator);

            foreach (var role in rolesToAdd)
            {
                var roleAssignment = new UserRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Role = role
                };
                _db.UserRoleAssignments.Add(roleAssignment);
            }

            _db.SaveChanges();

            // Auto-login the user
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("IsAuthenticated", "true");

            return RedirectToAction("Index", "Home");
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}

