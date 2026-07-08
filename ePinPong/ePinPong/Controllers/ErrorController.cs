using Microsoft.AspNetCore.Mvc;

namespace ePinPong.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    ViewBag.ErrorMessage = "Izvinite, stranica koju tražite nije pronađena.";
                    return View("NotFound");
            }

            return View("Error");
        }

        [Route("Error")]
        public IActionResult Error()
        {
            return View();
        }
    }
}
