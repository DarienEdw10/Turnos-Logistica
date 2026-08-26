using Microsoft.AspNetCore.Mvc;

namespace TurnosLogistica.MVC.Controllers;

public class CalendarioController : Controller
{
    public IActionResult Index(string agrupacion = "proyecto", string granularidad = "mensual")
    {
        ViewBag.Agrupacion = agrupacion;
        ViewBag.Granularidad = granularidad;
        return View();
    }
}