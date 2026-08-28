using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;

namespace TurnosLogistica.MVC.Controllers;

public class PlantasController : Controller
{
    private readonly AppDbContext _context;

    public PlantasController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int plantaActivaId = ObtenerPlantaActivaId();
        ViewBag.PlantaActivaId = plantaActivaId;

        var plantas = await _context.Plantas
            .OrderBy(p => p.Id)
            .ToListAsync();

        return View(plantas);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SeleccionarPlanta(int plantaId, string? returnUrl = null)
    {
        // Guardar la cookie con Path = "/" para que tenga alcance global en todo el dominio
        Response.Cookies.Append("PlantaActivaId", plantaId.ToString(), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/",
            IsEssential = true,
            HttpOnly = false,
            SameSite = SameSiteMode.Lax
        });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Calendario");
    }

    // Endpoint rápido para cambios desde el selector del Header (AJAX o Fetch)
    [HttpPost]
    public IActionResult CambiarPlantaAjax([FromBody] int plantaId)
    {
        Response.Cookies.Append("PlantaActivaId", plantaId.ToString(), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/",
            IsEssential = true,
            HttpOnly = false,
            SameSite = SameSiteMode.Lax
        });

        return Ok(new { success = true, plantaId });
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