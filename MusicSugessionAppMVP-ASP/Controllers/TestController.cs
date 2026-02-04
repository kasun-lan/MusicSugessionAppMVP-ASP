using Microsoft.AspNetCore.Mvc;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class TestController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
