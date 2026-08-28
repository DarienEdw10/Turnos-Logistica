using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Services;
using Logger = Magna.Cosma.Autotek.Log.Logger;

namespace TurnosLogistica.MVC.Controllers;

[Authorize]
public class UsuariosController : Controller
{
    private readonly AppDbContext _context;
    private readonly RepositorioEmpleados _repositorioEmpleados;
    private readonly Logger _logger;

    public UsuariosController(
        AppDbContext context,
        RepositorioEmpleados repositorioEmpleados,
        Logger logger)
    {
        _context = context;
        _repositorioEmpleados = repositorioEmpleados;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult BuscarColaboradoresCorporativos(string? query)
    {
        try
        {
            var empleados = _repositorioEmpleados.ObtenerEmpleados(soloActivos: true);
            string q = query?.Trim().ToUpper() ?? "";

            var filtrados = empleados
                .Where(e => e != null)
                .Select(e =>
                {
                    string cwidReal = "";

                    // 1. Extraer desde la colección CWIDs del objeto nativo de la DLL
                    if (e.CWIDs != null)
                    {
                        foreach (var c in e.CWIDs)
                        {
                            if (c == null) continue;

                            var propValor = c.GetType().GetProperty("Valor")?.GetValue(c)?.ToString()
                                         ?? c.GetType().GetProperty("Cuenta")?.GetValue(c)?.ToString()
                                         ?? c.GetType().GetProperty("Nombre")?.GetValue(c)?.ToString()
                                         ?? c.GetType().GetProperty("CWID")?.GetValue(c)?.ToString();

                            string raw = propValor ?? c.ToString() ?? "";

                            // Descartar namespaces de la DLL y hashes largos (>= 30 chars)
                            if (!string.IsNullOrWhiteSpace(raw) &&
                                !raw.Contains("Magna.Cosma.Autotek") &&
                                raw.Length < 30)
                            {
                                cwidReal = raw.Trim();
                                break;
                            }
                        }
                    }

                    // 2. Respaldo desde el correo corporativo
                    if (string.IsNullOrWhiteSpace(cwidReal) && e.Correos != null)
                    {
                        var correo = e.Correos.FirstOrDefault(corr => corr != null && !string.IsNullOrWhiteSpace(corr.Direccion));
                        if (correo != null && correo.Direccion.Contains('@'))
                        {
                            string prefijo = correo.Direccion.Split('@')[0].Trim();
                            if (prefijo.Length < 30)
                            {
                                cwidReal = prefijo;
                            }
                        }
                    }

                    string nombreCompleto = !string.IsNullOrWhiteSpace(e.NombrePropio)
                        ? e.NombrePropio.Trim()
                        : (e.NombrePorApellidos ?? $"{e.Nombre} {e.ApellidoPaterno}".Trim());

                    return new
                    {
                        numero = e.NumeroDeEmpleado,
                        nombre = nombreCompleto,
                       planta = e.Planta.ToString(),
                        cwid = cwidReal
                    };
                })
                .Where(e => string.IsNullOrEmpty(q)
                    || e.numero.ToString().Contains(q)
                    || e.nombre.ToUpper().Contains(q)
                    || (!string.IsNullOrEmpty(e.cwid) && e.cwid.ToUpper().Contains(q)))
                .Take(30)
                .ToList();

            return Json(new { success = true, data = filtrados });
        }
        catch (Exception ex)
        {
            _logger.Registrar(
                nivel: Logger.NivelesLog.Basico,
                tipo: Logger.TiposLog.Errores,
                origen: "UsuariosController.BuscarColaboradoresCorporativos",
                texto: $"Error al buscar colaboradores: {ex.Message}");

            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AsignarNivel([FromBody] AsignarNivelDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Cwid) || dto.Nivel < 10)
        {
            return BadRequest(new { success = false, message = "Datos inválidos para asignación de nivel." });
        }

        string cwidLimpio = dto.Cwid.Contains('\\') ? dto.Cwid.Split('\\')[1].Trim() : dto.Cwid.Trim();
        string usuarioActual = User?.Identity?.Name ?? Environment.UserName ?? "Sistemas";

        int plantaId = ObtenerPlantaActivaId();

        string rol = dto.Nivel switch
        {
            >= 40 => "sistemas",
            >= 30 => "admin",
            >= 20 => "jefe_log",
            _ => "operador"
        };

        try
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.CWID == cwidLimpio || u.NoEmpleado == dto.NoEmpleado);

            if (usuario != null)
            {
                usuario.CWID = cwidLimpio;
                usuario.Nivel = dto.Nivel;
                usuario.Rol = rol;
                usuario.Activo = true;
                if (!string.IsNullOrWhiteSpace(dto.Nombre)) usuario.Nombre = dto.Nombre;
            }
            else
            {
                usuario = new Usuario
                {
                    PlantaId = plantaId,
                    CWID = cwidLimpio,
                    NoEmpleado = string.IsNullOrWhiteSpace(dto.NoEmpleado) ? cwidLimpio : dto.NoEmpleado,
                    Nombre = string.IsNullOrWhiteSpace(dto.Nombre) ? cwidLimpio : dto.Nombre,
                    Email = $"{cwidLimpio.ToLower()}@autotek.com",
                    Rol = rol,
                    Nivel = dto.Nivel,
                    Activo = true,
                    CreadoAt = DateTime.UtcNow
                };
                _context.Usuarios.Add(usuario);
            }

            await _context.SaveChangesAsync();

            _logger.Registrar(
                nivel: Logger.NivelesLog.Detallado,
                tipo: Logger.TiposLog.Informativo,
                origen: "UsuariosController.AsignarNivel",
                texto: $"El usuario [{usuarioActual}] asignó Nivel [{dto.Nivel}] al colaborador [{dto.Nombre}] (CWID: [{dto.Cwid}]).");

            return Json(new { success = true, message = $"Permisos actualizados para {dto.Nombre} (Nivel {dto.Nivel} - {rol.ToUpper()})." });
        }
        catch (Exception ex)
        {
            string detalleError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

            _logger.Registrar(
                nivel: Logger.NivelesLog.Basico,
                tipo: Logger.TiposLog.Errores,
                origen: "UsuariosController.AsignarNivel",
                texto: $"Error al asignar nivel a [{dto.Cwid}]: {detalleError}");

            return StatusCode(500, new { success = false, message = detalleError });
        }
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

public class AsignarNivelDto
{
    public string Cwid { get; set; } = string.Empty;
    public string NoEmpleado { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Nivel { get; set; }
}