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
            .Where(p => p.Activa)
            .OrderBy(p => p.Id)
            .ToListAsync();

        return View(plantas);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SeleccionarPlanta(int plantaId, string? returnUrl = null)
    {
        EstablecerCookiePlanta(plantaId);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Calendario", new { plantaId });
    }

    // Endpoint rápido para cambios desde el selector del Header (soporta JSON { plantaId: X })
    [HttpPost]
    public IActionResult CambiarPlantaAjax([FromBody] CambiarPlantaDto? dto)
    {
        if (dto == null || dto.PlantaId <= 0)
        {
            return BadRequest(new { success = false, message = "Identificador de planta inválido." });
        }

        EstablecerCookiePlanta(dto.PlantaId);

        return Ok(new { success = true, plantaId = dto.PlantaId });
    }

    private void EstablecerCookiePlanta(int plantaId)
    {
        Response.Cookies.Append("PlantaActivaId", plantaId.ToString(), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/",
            IsEssential = true,
            HttpOnly = false,
            SameSite = SameSiteMode.Lax
        });
    }

    private int ObtenerPlantaActivaId()
    {
        if (Request.Cookies.TryGetValue("PlantaActivaId", out string? idStr) && int.TryParse(idStr, out int idVal))
        {
            return idVal;
        }
        return 1;
    }

    public class CambiarPlantaDto
    {
        public int PlantaId { get; set; }
    }
}