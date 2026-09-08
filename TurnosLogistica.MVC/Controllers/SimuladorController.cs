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
    public async Task<IActionResult> CambiarUsuarioPorNomina([FromForm] string? nomina, [FromBody] CambiarNominaDto? dtoJson, [FromQuery] string? returnUrl = null)
    {
        string? valorNomina = nomina ?? dtoJson?.Nomina;

        if (string.IsNullOrWhiteSpace(valorNomina))
        {
            return BadRequest(new { success = false, message = "El número de control/CWID es requerido." });
        }

        string busqueda = valorNomina.Trim();

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => (u.NoEmpleado == busqueda || u.CWID == busqueda || u.Email.StartsWith(busqueda)) && u.Activo);

        if (usuario == null)
        {
            return NotFound(new { success = false, message = $"No se encontró ningún usuario activo con Nómina/CWID: {busqueda}" });
        }

        string cwidCookie = !string.IsNullOrWhiteSpace(usuario.NoEmpleado) ? usuario.NoEmpleado : (usuario.CWID ?? busqueda);

        // 1. Cookie para que IdentificacionUsuarioMiddleware reconstruya los Claims en la siguiente petición
        Response.Cookies.Append("Simulador_CWID", cwidCookie, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            SameSite = SameSiteMode.Lax,
            HttpOnly = false
        });

        // 2. Alinear la planta activa con la planta asignada nativa del usuario
        Response.Cookies.Append("PlantaActivaId", usuario.PlantaId.ToString(), new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            SameSite = SameSiteMode.Lax,
            HttpOnly = false
        });

        // Respuesta para llamadas vía Fetch/AJAX
        if (Request.Headers.Accept.ToString().Contains("application/json") || Request.ContentType?.Contains("application/json") == true)
        {
            return Ok(new
            {
                success = true,
                cwid = cwidCookie,
                nombre = usuario.Nombre,
                plantaId = usuario.PlantaId,
                rol = usuario.Rol,
                nivel = usuario.Nivel
            });
        }

        // Respuesta para llamadas POST vía formulario
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Calendario", new { plantaId = usuario.PlantaId });
    }

    public class CambiarNominaDto
    {
        public string Nomina { get; set; } = string.Empty;
    }
}