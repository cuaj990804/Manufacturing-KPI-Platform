using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GDIKPI.Controllers
{
    [Authorize]
    public class AttendanceDashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
