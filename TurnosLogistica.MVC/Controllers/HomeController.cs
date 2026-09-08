using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        // Redirige directamente al Calendario de Producción con la planta activa
        int plantaId = ObtenerPlantaActivaId();
        return RedirectToAction("Index", "Calendario", new { plantaId = plantaId });
    }

    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private int ObtenerPlantaActivaId()
    {
        if (Request.Cookies.TryGetValue("PlantaActivaId", out string? idStr) && int.TryParse(idStr, out int idVal))
        {
            return idVal;
        }

        string? claimPlanta = User.FindFirst("PlantaAsignadaId")?.Value;
        if (int.TryParse(claimPlanta, out int idClaim))
        {
            return idClaim;
        }

        return 1;
    }
}