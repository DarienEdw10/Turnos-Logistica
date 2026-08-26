using Microsoft.AspNetCore.Mvc;

namespace TurnosLogistica.MVC.Controllers;

public class ProgramacionController : Controller
{
    public IActionResult Index() => View();

    [HttpPost]
    public IActionResult Guardar(string proyecto, string linea, string celda, string estacion, DateTime fecha, string turno, double tiempoEstimado, string razon)
    {
        TempData["Success"] = "Programación de producción registrada correctamente.";
        return RedirectToAction("Index", "Calendario");
    }
}