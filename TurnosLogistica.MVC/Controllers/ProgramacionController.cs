using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
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
        // 1. Obtener planta activa desde parámetro de URL o Cookie (default 1)
        int plantaActiva = plantaId
            ?? (Request.Cookies.TryGetValue("PlantaActivaId", out string? cookieVal) && int.TryParse(cookieVal, out int parsedId) ? parsedId : 1);

        ViewBag.PlantaActivaId = plantaActiva;

        // 2. Cargar Proyectos de esa planta
        var proyectos = await _service.ObtenerProyectosPorPlantaAsync(plantaActiva);
        ViewBag.Proyectos = proyectos.ToList();

        // 3. Cargar Turnos EXCLUSIVOS de esa planta
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
    public async Task<IActionResult> Guardar(ProgramacionFormViewModel model)
    {
        int plantaId = ObtenerPlantaActivaId();

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

        await _service.GuardarProgramacionAsync(model, usuarioId: 1);
        return RedirectToAction("Index", "Calendario", new { mes = model.FechaProduccion.Month, anio = model.FechaProduccion.Year });
    }

    private int ObtenerPlantaActivaId()
    {
        if (Request.Cookies.TryGetValue("PlantaActivaId", out string? idStr) && int.TryParse(idStr, out int idVal))
        {
            return idVal;
        }
        return 1;
    }
    public class ProgramacionMasivaDto
    {
        public List<int> CeldaIds { get; set; } = new();
        public List<DateTime> Fechas { get; set; } = new();
        public int TurnoId { get; set; }
        public int ParteId { get; set; }
        public decimal HorasNetas { get; set; }
        public decimal JphPlaneado { get; set; }
        public int LotePlaneado { get; set; }
        public string? RazonCambio { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> GuardarProgramacionMasiva([FromBody] ProgramacionMasivaDto dto)
    {
        if (dto == null || !dto.CeldaIds.Any() || !dto.Fechas.Any() || dto.TurnoId <= 0)
        {
            return BadRequest(new { success = false, message = "Debe seleccionar al menos una celda, una fecha y un turno válido." });
        }

        int registrosProcesados = 0;

        try
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                foreach (var celdaId in dto.CeldaIds)
                {
                    foreach (var fecha in dto.Fechas)
                    {
                        var model = new ProgramacionFormViewModel
                        {
                            CeldaId = celdaId,
                            FechaProduccion = fecha.Date,
                            TurnoId = dto.TurnoId,
                            NumeroParteId = dto.ParteId,
                            TiempoEstimadoHoras = (double)dto.HorasNetas,
                            CantidadProgramada = dto.LotePlaneado, // <-- Asigna la cantidad escrita
                            RazonObligatoria = dto.RazonCambio ?? "Programación masiva en lote"
                        };

                        await _service.GuardarProgramacionAsync(model, usuarioId: 1);
                        registrosProcesados++;
                    }
                }
            });

            return Json(new
            {
                success = true,
                message = $"Programación procesada con éxito ({registrosProcesados} turnos asignados)."
            });
        }
        catch (Exception ex)
        {
            string detalle = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return StatusCode(500, new { success = false, message = $"Error al procesar la programación: {detalle}" });
        }
    }

}