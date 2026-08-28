using Microsoft.AspNetCore.Mvc;
using TurnosLogistica.MVC.Services;

namespace TurnosLogistica.MVC.Controllers;

public class CalendarioController : Controller
{
    private readonly IPlanificacionService _service;

    public CalendarioController(IPlanificacionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? plantaId,
        string agrupacion = "proyecto",
        string granularidad = "mensual",
        int? mes = null,
        int? anio = null,
        string? linea = null,
        string? celda = null,
        string? turno = null)
    {
        // Si no viene en la URL, se lee de la cookie global del layout
        int pId = plantaId ?? ObtenerPlantaActivaId();
        int m = mes ?? DateTime.Today.Month;
        int a = anio ?? DateTime.Today.Year;

        var vm = await _service.ObtenerCalendarioAsync(pId, agrupacion, granularidad, m, a, linea, celda, turno);
        return View(vm);
    }

    private int ObtenerPlantaActivaId()
    {
        if (Request.Cookies.TryGetValue("PlantaActivaId", out string? idStr) && int.TryParse(idStr, out int idVal))
        {
            return idVal;
        }
        return 1;
    }
}