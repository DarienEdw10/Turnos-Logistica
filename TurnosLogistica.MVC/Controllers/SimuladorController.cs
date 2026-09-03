using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;

namespace TurnosLogistica.MVC.Controllers;

public class SimuladorController : Controller
{
    private readonly AppDbContext _context;

    public SimuladorController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CambiarUsuarioPorNomina(string nomina, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(nomina))
            return BadRequest("El número de control/CWID es requerido.");

        string busqueda = nomina.Trim();

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => (u.NoEmpleado == busqueda || u.CWID == busqueda) && u.Activo);

        if (usuario == null)
            return NotFound($"No se encontró ningún usuario activo con el número de control/CWID: {nomina}");

        // Guardar cookies para la vista y la sesión activa del simulador
        Response.Cookies.Append("Simulador_CWID", usuario.NoEmpleado, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.Now.AddDays(7),
            SameSite = SameSiteMode.Lax
        });

        Response.Cookies.Append("PlantaActivaId", usuario.PlantaId.ToString(), new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.Now.AddDays(7),
            SameSite = SameSiteMode.Lax
        });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Calendario");
    }
}