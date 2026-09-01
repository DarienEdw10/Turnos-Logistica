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

    private async Task<(int Nivel, string Rol, string CWID)> ObtenerUsuarioActualAsync()
    {
        string cwid = User?.Identity?.Name ?? Environment.UserName ?? "darienedwin.jimenez";
        if (cwid.Contains('\\')) cwid = cwid.Split('\\')[1].Trim();

        var u = await _context.Usuarios.FirstOrDefaultAsync(x => x.CWID == cwid || x.NoEmpleado == cwid);
        int nivel = u?.Nivel ?? 40; // Por defecto 40 si es el usuario simulado de pruebas
        string rol = u?.Rol ?? (nivel >= 40 ? "sistemas" : (nivel >= 30 ? "admin" : "operador"));

        return (nivel, rol, cwid);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var (nivel, rol, _) = await ObtenerUsuarioActualAsync();

        // Solo Administradores (30+) y Sistemas (40) tienen acceso a gestionar usuarios
        if (nivel < 30)
        {
            return Forbid();
        }

        ViewBag.NivelUsuarioActual = nivel;
        ViewBag.EsSistemas = (rol == "sistemas" || nivel >= 40);

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> BuscarColaboradoresCorporativos(string? query)
    {
        var (nivel, _, _) = await ObtenerUsuarioActualAsync();
        if (nivel < 30) return Forbid();

        try
        {
            var empleados = _repositorioEmpleados.ObtenerEmpleados(soloActivos: true);
            string q = query?.Trim().ToUpper() ?? "";

            var filtrados = empleados
                .Where(e => e != null)
                .Select(e =>
                {
                    string cwidReal = "";

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

                            if (!string.IsNullOrWhiteSpace(raw) &&
                                !raw.Contains("Magna.Cosma.Autotek") &&
                                raw.Length < 30)
                            {
                                cwidReal = raw.Trim();
                                break;
                            }
                        }
                    }

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
        var (nivelEjecutor, rolEjecutor, usuarioActual) = await ObtenerUsuarioActualAsync();

        if (nivelEjecutor < 30)
        {
            return Forbid();
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Cwid) || dto.Nivel < 10)
        {
            return BadRequest(new { success = false, message = "Datos inválidos para asignación de nivel." });
        }

        // =========================================================================
        // REGLA: El Administrador (30) solo asigna niveles menores al suyo (< 30)
        //        Solo Sistemas (40) puede asignar Nivel 30 (Admin) o Nivel 40 (Sistemas)
        // =========================================================================
        if (nivelEjecutor < 40 && dto.Nivel >= nivelEjecutor)
        {
            return BadRequest(new 
            { 
                success = false, 
                message = "Permiso denegado: Un Administrador solo puede asignar roles de menor jerarquía (Operador o Jefe de Logística). Solo Sistemas puede asignar Administradores." 
            });
        }

        string cwidLimpio = dto.Cwid.Contains('\\') ? dto.Cwid.Split('\\')[1].Trim() : dto.Cwid.Trim();
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
                // Si el usuario a editar ya tiene un nivel igual o mayor al ejecutor, bloquear si no es Sistemas
                if (nivelEjecutor < 40 && usuario.Nivel >= nivelEjecutor)
                {
                    return BadRequest(new { success = false, message = "No tiene permisos para modificar a un usuario de nivel igual o superior al suyo." });
                }

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
                texto: $"El usuario [{usuarioActual}] ({rolEjecutor}) asignó Nivel [{dto.Nivel}] al colaborador [{dto.Nombre}] (CWID: [{dto.Cwid}]).");

            return Json(new { success = true, message = $"Permisos actualizados para {dto.Nombre} (Rol: {rol.ToUpper()})." });
        }
        catch (Exception ex)
        {
            string detalleError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
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
    public class AsignarNivelDto
{
    public string Cwid { get; set; } = string.Empty;
    public string NoEmpleado { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Nivel { get; set; }
}
}