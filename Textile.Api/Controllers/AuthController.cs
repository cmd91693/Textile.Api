using Microsoft.AspNetCore.Mvc;

namespace Textile.Api.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
