using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Filters;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Controllers;

public class TurnosController : Controller
{
    private readonly AppDbContext _context;

    public TurnosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // 1. Candado de nivel: solo jefe_log, admin y sistemas (Nivel >= 20)
        if (!TienePermisoCatalogo())
        {
            return Forbid();
        }

        int plantaId = ObtenerPlantaActivaId();
        ViewBag.PlantaActivaId = plantaId;

        // 2. Obtener turnos de la planta activa
        var turnosDb = await _context.Turnos
            .AsNoTracking()
            .Where(t => t.PlantaId == plantaId)
            .OrderBy(t => t.HoraInicio)
            .ToListAsync();

        var turnoIds = turnosDb.Select(t => t.Id).ToList();

        // 3. Cargar paros base de catálogo (sin orden de programación específica)
        var parosBase = await _context.TurnoParos
            .AsNoTracking()
            .Where(p => p.TurnoId.HasValue 
                        && turnoIds.Contains(p.TurnoId.Value) 
                        && p.ProgramacionId == null 
                        && p.Activo)
            .ToListAsync();

        var turnos = turnosDb.Select(t =>
        {
            double duracion = t.HoraFin <= t.HoraInicio
                ? (t.HoraFin.Add(TimeSpan.FromDays(1)) - t.HoraInicio).TotalHours
                : (t.HoraFin - t.HoraInicio).TotalHours;

            string clave = "T1";
            string claseCss = "t1";
            string colorHex = "#2563eb";
            var upper = (t.Nombre ?? string.Empty).ToUpperInvariant();

            if (upper.Contains("2") || upper.Contains("VESP"))
            {
                clave = "T2";
                claseCss = "t2";
                colorHex = "#d97706";
            }
            else if (upper.Contains("3") || upper.Contains("NOCT") || upper.Contains("12H-N"))
            {
                clave = "T3";
                claseCss = "t3";
                colorHex = "#1e293b";
            }

            var parosTurno = parosBase
                .Where(p => p.TurnoId == t.Id)
                .Select(p => new TurnoParoItemViewModel
                {
                    Id = p.Id,
                    TurnoId = t.Id,
                    TipoParo = p.TipoParo,
                    DuracionMinutos = p.DuracionMinutos,
                    EsProgramado = p.EsProgramado,
                    Activo = p.Activo
                }).ToList();

            return new TurnoViewModel
            {
                Id = t.Id,
                PlantaId = t.PlantaId,
                Clave = clave,
                Nombre = t.Nombre ?? string.Empty,
                HoraInicio = t.HoraInicio,
                HoraFin = t.HoraFin,
                Horas = Math.Round(duracion, 2),
                ClaseColor = claseCss,
                ColorHex = colorHex,
                Activo = t.Activo,
                Paros = parosTurno
            };
        }).ToList();

        return View(turnos);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTurno(int id)
    {
        var t = await _context.Turnos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();

        var paros = await _context.TurnoParos
            .AsNoTracking()
            .Where(p => p.TurnoId == id && p.ProgramacionId == null && p.Activo)
            .Select(p => new
            {
                id = p.Id,
                tipoParo = p.TipoParo,
                duracionMinutos = p.DuracionMinutos,
                esProgramado = p.EsProgramado
            })
            .ToListAsync();

        return Json(new
        {
            id = t.Id,
            nombre = t.Nombre ?? string.Empty,
            horaInicio = t.HoraInicio.ToString(@"hh\:mm"),
            horaFin = t.HoraFin.ToString(@"hh\:mm"),
            activo = t.Activo,
            paros
        });
    }

    public class TurnoFormDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public bool Activo { get; set; }
        public List<TurnoParoInputDto> Paros { get; set; } = new();
    }

    public class TurnoParoInputDto
    {
        public int Id { get; set; }
        public string TipoParo { get; set; } = string.Empty;
        public int DuracionMinutos { get; set; }
        public bool EsProgramado { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ServiceFilter(typeof(ValidarOperacionPlantaAttribute))] // <-- Candado Multi-Planta
    public async Task<IActionResult> Guardar(TurnoFormDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Nombre))
        {
            return RedirectToAction(nameof(Index));
        }

        int plantaId = ObtenerPlantaActivaId();
        Turno? turno;
        string nombreLimpio = (model.Nombre ?? string.Empty).Trim();

        if (model.Id == 0)
        {
            turno = new Turno
            {
                PlantaId = plantaId,
                Nombre = nombreLimpio,
                HoraInicio = model.HoraInicio,
                HoraFin = model.HoraFin,
                Activo = model.Activo
            };
            _context.Turnos.Add(turno);
            await _context.SaveChangesAsync();
        }
        else
        {
            // Validar pertenencia a la planta activa
            turno = await _context.Turnos.FirstOrDefaultAsync(t => t.Id == model.Id && t.PlantaId == plantaId);
            if (turno == null) return NotFound();

            turno.Nombre = nombreLimpio;
            turno.HoraInicio = model.HoraInicio;
            turno.HoraFin = model.HoraFin;
            turno.Activo = model.Activo;
            await _context.SaveChangesAsync();

            var parosPrevios = await _context.TurnoParos
                .Where(p => p.TurnoId == turno.Id && p.ProgramacionId == null)
                .ToListAsync();

            _context.TurnoParos.RemoveRange(parosPrevios);
            await _context.SaveChangesAsync();
        }

        if (model.Paros != null && model.Paros.Any())
        {
            foreach (var p in model.Paros)
            {
                if (string.IsNullOrWhiteSpace(p.TipoParo) || p.DuracionMinutos <= 0)
                    continue;

                _context.TurnoParos.Add(new TurnoParo
                {
                    TurnoId = turno.Id,
                    ProgramacionId = null,
                    TipoParo = p.TipoParo.Trim(),
                    Descripcion = p.TipoParo.Trim(),
                    DuracionMinutos = p.DuracionMinutos,
                    EsProgramado = p.EsProgramado,
                    Activo = true
                });
            }
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private bool TienePermisoCatalogo()
    {
        string? nivelStr = User.FindFirst("NivelJerarquico")?.Value;
        string rol = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value?.ToLower() ?? string.Empty;

        if (int.TryParse(nivelStr, out int nivel) && nivel >= 20)
        {
            return true;
        }

        return rol == "jefe_log" || rol == "admin" || rol == "sistemas";
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