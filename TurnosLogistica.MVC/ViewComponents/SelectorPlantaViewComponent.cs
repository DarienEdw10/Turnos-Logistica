using Microsoft.AspNetCore.Mvc;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.Domain.Repositories;

namespace TurnosLogistica.MVC.ViewComponents;

public class SelectorPlantaViewComponent : ViewComponent
{
    private readonly IRepository<Planta> _plantaRepo;

    public SelectorPlantaViewComponent(IRepository<Planta> plantaRepo)
    {
        _plantaRepo = plantaRepo;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // 1. Obtener todas las plantas activas desde la base de datos
        var plantas = await _plantaRepo.FindAsync(p => p.Activa);

        // 2. Determinar la planta seleccionada (por QueryString, Cookie o primer registro)
        int plantaSeleccionada = 1;
        if (Request.Query.TryGetValue("plantaId", out var qVal) && int.TryParse(qVal, out int parsedQuery))
        {
            plantaSeleccionada = parsedQuery;
        }
        else if (Request.Cookies.TryGetValue("PlantaActivaId", out var cVal) && int.TryParse(cVal, out int parsedCookie))
        {
            plantaSeleccionada = parsedCookie;
        }
        else if (plantas.Any())
        {
            plantaSeleccionada = plantas.First().Id;
        }

        ViewBag.PlantaActivaId = plantaSeleccionada;
        return View(plantas.OrderBy(p => p.Id).ToList());
    }
}