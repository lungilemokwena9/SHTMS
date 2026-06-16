using Microsoft.AspNetCore.Mvc;

namespace SHTMS.Web.Controllers
{
    public class HomeController : Controller
    {
        // GET: / — Public landing page (no auth required)
        [Route("/")]
        public IActionResult Index()
        {
            // If already authenticated, redirect to dashboard
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            return View();
        }

        // GET: /Home/Error
        [Route("/Home/Error")]
        public IActionResult Error()
        {
            return View();
        }
    }
}