using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Filters;
using TurnosLogistica.MVC.Models;
using TurnosLogistica.MVC.Services;

namespace TurnosLogistica.MVC.Controllers;

public class ProgramacionController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPlanificacionService _service;

    public ProgramacionController(AppDbContext context, IPlanificacionService service)
    {
        _context = context;
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? plantaId)
    {
        int plantaActiva = plantaId
            ?? (Request.Cookies.TryGetValue("PlantaActivaId", out string? cookieVal) && int.TryParse(cookieVal, out int parsedId) ? parsedId : 1);

        ViewBag.PlantaActivaId = plantaActiva;

        var proyectos = await _service.ObtenerProyectosPorPlantaAsync(plantaActiva);
        ViewBag.Proyectos = proyectos.ToList();

        var turnos = await _service.ObtenerTurnosPorPlantaAsync(plantaActiva);
        ViewBag.Turnos = turnos.OrderBy(t => t.HoraInicio).Select(t => new
        {
            t.Id,
            t.Nombre,
            Horario = $"{t.HoraInicio:hh\\:mm} – {t.HoraFin:hh\\:mm}",
            Horas = t.HoraFin <= t.HoraInicio
                ? (t.HoraFin.Add(TimeSpan.FromDays(1)) - t.HoraInicio).TotalHours
                : (t.HoraFin - t.HoraInicio).TotalHours,
            ClaseColor = t.Nombre.Contains("1") ? "matutino" : (t.Nombre.Contains("2") ? "vespertino" : "nocturno")
        }).ToList();

        return View(new ProgramacionFormViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerLineasPorProyecto(int proyectoId)
    {
        var lineas = await _context.Lineas
            .Where(l => l.ProyectoId == proyectoId && l.Activa)
            .Select(l => new { id = l.Id, texto = l.Nombre ?? l.Codigo })
            .ToListAsync();
        return Json(lineas);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCeldasPorLinea(int lineaId)
    {
        var celdas = await _context.Celdas
            .Where(c => c.LineaId == lineaId && c.Activa)
            .Select(c => new { id = c.Id, texto = c.Codigo + " — " + c.Nombre })
            .ToListAsync();
        return Json(celdas);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerPartesPorCelda(int celdaId)
    {
        var partes = await _context.NumerosDeParte
            .Where(p => p.CeldaId == celdaId && p.Activo)
            .Select(p => new { id = p.Id, texto = p.SapPartNumber + " — " + p.Descripcion })
            .ToListAsync();
        return Json(partes);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTurnosPorPlanta(int? plantaId = null)
    {
        int pId = plantaId ?? ObtenerPlantaActivaId();

        var turnosDb = await _context.Turnos
            .Where(t => t.PlantaId == pId && t.Activo)
            .OrderBy(t => t.HoraInicio)
            .ToListAsync();

        var result = turnosDb.Select(t => new
        {
            id = t.Id,
            nombre = t.Nombre,
            horario = $"{t.HoraInicio:hh\\:mm} – {t.HoraFin:hh\\:mm}",
            horas = Math.Round(t.DuracionHoras, 1),
            claseColor = t.ClaseColor
        });

        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(ValidarOperacionPlantaAttribute))] // <-- Candado Multi-Planta
    public async Task<IActionResult> Guardar(ProgramacionFormViewModel model)
    {
        int plantaId = ObtenerPlantaActivaId();
        int usuarioId = ObtenerUsuarioSesionId();

        if (!ModelState.IsValid)
        {
            ViewBag.Proyectos = await _context.Proyectos
                .Where(p => p.PlantaId == plantaId && p.Activo)
                .OrderBy(p => p.Codigo)
                .ToListAsync();

            var turnosDb = await _context.Turnos
                .Where(t => t.PlantaId == plantaId && t.Activo)
                .OrderBy(t => t.HoraInicio)
                .ToListAsync();

            ViewBag.Turnos = turnosDb.Select(t => new
            {
                Id = t.Id,
                Nombre = t.Nombre,
                Horario = $"{t.HoraInicio:hh\\:mm} – {t.HoraFin:hh\\:mm}",
                Horas = Math.Round(t.DuracionHoras, 1),
                ClaseColor = t.ClaseColor
            }).ToList();

            return View("Index", model);
        }

        await _service.GuardarProgramacionAsync(model, usuarioId: usuarioId);
        return RedirectToAction("Index", "Calendario", new { mes = model.FechaProduccion.Month, anio = model.FechaProduccion.Year });
    }

    [HttpPost]
    [ServiceFilter(typeof(ValidarOperacionPlantaAttribute))] // <-- Candado Multi-Planta
    public async Task<IActionResult> GuardarProgramacionMasiva([FromBody] ProgramacionMasivaDto dto)
    {
        if (dto == null || !dto.Asignaciones.Any() || !dto.Fechas.Any() || dto.TurnoId <= 0)
        {
            return BadRequest(new { success = false, message = "Debe asignar al menos una celda válida, una fecha y un turno." });
        }

        int usuarioId = ObtenerUsuarioSesionId();
        int registrosProcesados = 0;

        try
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                foreach (var item in dto.Asignaciones)
                {
                    if (item.ParteId <= 0 || item.CantidadPiezas <= 0) continue;

                    foreach (var fecha in dto.Fechas)
                    {
                        var model = new ProgramacionFormViewModel
                        {
                            CeldaId = item.CeldaId,
                            FechaProduccion = fecha.Date,
                            TurnoId = dto.TurnoId,
                            NumeroParteId = item.ParteId,
                            TiempoEstimadoHoras = (double)dto.HorasNetas,
                            CantidadProgramada = item.CantidadPiezas,
                            RazonObligatoria = dto.RazonCambio ?? "Programación masiva balanceada"
                        };

                        await _service.GuardarProgramacionAsync(model, usuarioId: usuarioId);
                        registrosProcesados++;
                    }
                }
            });

            return Json(new
            {
                success = true,
                message = $"Programación procesada con éxito ({registrosProcesados} órdenes generadas)."
            });
        }
        catch (Exception ex)
        {
            string detalle = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return StatusCode(500, new { success = false, message = $"Error al procesar: {detalle}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerPartesPorCeldas([FromQuery] List<int> celdaIds)
    {
        if (celdaIds == null || !celdaIds.Any())
            return Json(new List<object>());

        var celdaIdsNullable = celdaIds.Select(id => (int?)id).ToList();

        var partes = await (from p in _context.NumerosDeParte.AsNoTracking()
                            where p.CeldaId != null
                                  && celdaIdsNullable.Contains(p.CeldaId)
                                  && p.Activo
                            join c in _context.Celdas.AsNoTracking()
                                 on p.CeldaId equals (int?)c.Id
                            orderby c.Codigo, p.SapPartNumber
                            select new
                            {
                                id = p.Id,
                                texto = p.SapPartNumber + " — " + p.Descripcion,
                                celdaId = p.CeldaId ?? 0,
                                celdaCodigo = c.Codigo ?? ("Celda " + c.Id)
                            }).ToListAsync();

        return Json(partes);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerParosYHorasTurno(int turnoId)
    {
        var turno = await _context.Turnos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == turnoId);

        if (turno == null)
            return NotFound(new { message = "Turno no encontrado." });

        double horasBrutas = Math.Round(turno.DuracionHoras, 2);

        var paros = await _context.TurnoParos
            .AsNoTracking()
            .Where(p => p.TurnoId == turnoId && p.EsProgramado && p.Activo)
            .Select(p => new
            {
                id = p.Id,
                tipoParo = p.TipoParo,
                descripcion = p.Descripcion ?? p.TipoParo,
                duracionMinutos = p.DuracionMinutos
            })
            .ToListAsync();

        double totalMinutosParo = paros.Sum(p => p.duracionMinutos);
        double horasParos = Math.Round(totalMinutosParo / 60.0, 2);
        double horasNetas = Math.Max(0, Math.Round(horasBrutas - horasParos, 2));

        return Json(new
        {
            turnoId = turno.Id,
            nombre = turno.Nombre,
            horasBrutas,
            horasParos,
            horasNetas,
            tieneParos = paros.Any(),
            paros
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

    private int ObtenerUsuarioSesionId()
    {
        string? claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(claimId, out int id))
        {
            return id;
        }
        return 1;
    }

    public class ItemAsignacionCeldaDto
    {
        public int CeldaId { get; set; }
        public int ParteId { get; set; }
        public int CantidadPiezas { get; set; }
    }

    public class ProgramacionMasivaDto
    {
        public List<ItemAsignacionCeldaDto> Asignaciones { get; set; } = new();
        public List<DateTime> Fechas { get; set; } = new();
        public int TurnoId { get; set; }
        public decimal HorasNetas { get; set; }
        public string? RazonCambio { get; set; }
    }
}