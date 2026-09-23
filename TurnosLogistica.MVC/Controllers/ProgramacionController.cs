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
    [ServiceFilter(typeof(ValidarOperacionPlantaAttribute))]
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
    [ServiceFilter(typeof(ValidarOperacionPlantaAttribute))]
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

                        // 1. Guardar la programación principal
                        await _service.GuardarProgramacionAsync(model, usuarioId: usuarioId);
                        registrosProcesados++;

                        // 2. RECUPERAR la programación recién creada
                        var programacionCreada = await _context.Programaciones
                            .OrderByDescending(p => p.Id)
                            .FirstOrDefaultAsync(p => p.Fecha.Date == fecha.Date 
                                                   && p.TurnoId == dto.TurnoId 
                                                   && p.NumeroParteId == item.ParteId);

                        if (programacionCreada != null && dto.ParosTemporales != null && dto.ParosTemporales.Any())
                        {
                            foreach (var pt in dto.ParosTemporales)
                            {
                                bool existe = await _context.TurnoParos.AnyAsync(tp => 
                                    tp.ProgramacionId == programacionCreada.Id && 
                                    tp.TipoParo == pt.Descripcion && 
                                    tp.DuracionMinutos == pt.DuracionMinutos);

                                if (!existe)
                                {
                                    _context.TurnoParos.Add(new TurnoParo
                                    {
                                        ProgramacionId = programacionCreada.Id,
                                        TurnoId = dto.TurnoId,
                                        TipoParo = pt.Descripcion,
                                        Descripcion = pt.Descripcion,
                                        DuracionMinutos = pt.DuracionMinutos,
                                        EsProgramado = true, 
                                        CategoriaParo = 2, // 2 = Paro Temporal / Programado desde el formulario
                                        Activo = true
                                    });
                                }
                            }
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            });

            return Json(new
            {
                success = true,
                message = $"Programación procesada y paros temporales registrados con éxito ({registrosProcesados} órdenes generadas)."
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
    public async Task<IActionResult> ObtenerParosYHorasTurno(int turnoId, string? fecha = null)
    {
        var turno = await _context.Turnos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == turnoId);

        if (turno == null)
            return NotFound(new { message = "Turno no encontrado." });

        double horasBrutas = Math.Round(turno.DuracionHoras, 2);

        // Obtener paros base del turno de forma robusta
        var parosBase = await _context.TurnoParos
            .AsNoTracking()
            .Where(p => p.TurnoId == turnoId && p.Activo && p.ProgramacionId == null && (p.CategoriaParo == 1 || p.CategoriaParo == 0 || p.EsProgramado))
            .Select(p => new
            {
                id = p.Id,
                tipoParo = p.TipoParo,
                descripcion = p.Descripcion ?? p.TipoParo,
                duracionMinutos = p.DuracionMinutos,
                categoriaParo = p.CategoriaParo > 0 ? p.CategoriaParo : (byte)1 // Aseguramos categoría 1 por defecto para los base
            })
            .ToListAsync();

        double totalMinutosParo = parosBase.Sum(p => (int)p.duracionMinutos);
        double horasParos = Math.Round(totalMinutosParo / 60.0, 2);
        double horasNetas = Math.Max(0, Math.Round(horasBrutas - horasParos, 2));

        return Json(new
        {
            turnoId = turno.Id,
            nombre = turno.Nombre,
            horasBrutas,
            horasParos,
            horasNetas,
            tieneParos = parosBase.Any(),
            paros = parosBase
        });
    }
    [HttpGet]
public async Task<IActionResult> ObtenerJerarquiaPorCelda(int celdaId)
{
    var celda = await _context.Celdas.FindAsync(celdaId);
    if (celda == null) return NotFound();

    var linea = await _context.Lineas.FindAsync(celda.LineaId);
    if (linea == null) return NotFound();

    return Json(new {
        proyectoId = linea.ProyectoId ?? 0,
        lineaId = linea.Id,
        celdaId = celda.Id
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

    public class ParoTemporalDto
    {
        public string Descripcion { get; set; } = string.Empty;
        public int DuracionMinutos { get; set; }
    }

    public class ProgramacionMasivaDto
    {
        public List<ItemAsignacionCeldaDto> Asignaciones { get; set; } = new();
        public List<DateTime> Fechas { get; set; } = new();
        public int TurnoId { get; set; }
        public decimal HorasNetas { get; set; }
        public string? RazonCambio { get; set; }
        public List<ParoTemporalDto> ParosTemporales { get; set; } = new();
    }
}