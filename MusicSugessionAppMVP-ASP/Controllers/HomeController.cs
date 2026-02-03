using Microsoft.AspNetCore.Mvc;
using MusicSugessionAppMVP_ASP.Models;
using MusicSugessionAppMVP_ASP.Persistance;
using System.Diagnostics;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly Persistence _db;

        public HomeController(ILogger<HomeController> logger, Persistence db)
        {
            _logger = logger;
            _db = db;
        }

        public IActionResult Index()
        {
            var isAuthenticated =
                HttpContext.Session.GetString("IsAuthenticated") == "true";

            if (isAuthenticated)
            {
                ViewBag.IsAuthenticated = true;
                ViewBag.UserName = HttpContext.Session.GetString("UserName");
            }
            return View();
        }


        public IActionResult Menu()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AuthStatus()
        {
            var isAuthenticated =
                HttpContext.Session.GetString("IsAuthenticated") == "true";

            return Json(new
            {
                isAuthenticated
            });
        }

        public IActionResult Login(string? returnUrl = null)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") == "true")
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }






        [HttpPost]
        public IActionResult Login(string email, string password, string? returnUrl)
        {
            if (HttpContext.Session.GetString("IsAuthenticated") == "true")
            {
                return RedirectToAction("Index", "Home");
            }


            //check whether user exists in the database
            var user = _db.Users.FirstOrDefault(u => u.Email == email); //TODO : check for password as well.
            if (user == null)
            {
                ViewBag.Error = "User not found";
                return RedirectToAction("Login", "Home");
            }

            HttpContext.Session.SetString("Email", email);
            HttpContext.Session.SetString("IsAuthenticated", "true");
            HttpContext.Session.SetString("UserName", user.Name);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");

        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
