using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Services;

namespace TurnosLogistica.MVC.Controllers;

public class CalendarioController : Controller
{
    private readonly IPlanificacionService _service;
    private readonly AppDbContext _context;

    public CalendarioController(IPlanificacionService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? plantaId,
        string agrupacion = "proyecto",
        string granularidad = "mensual",
        int? mes = null,
        int? anio = null,
        string? linea = null,
        string? celda = null,
        string? turno = null)
    {
        int pId = plantaId ?? ObtenerPlantaActivaId();
        int m = mes ?? DateTime.Today.Month;
        int a = anio ?? DateTime.Today.Year;

        var vm = await _service.ObtenerCalendarioAsync(pId, agrupacion, granularidad, m, a, linea, celda, turno);
        return View(vm);
    }

    private int ObtenerPlantaActivaId()
    {
        if (Request.Cookies.TryGetValue("PlantaActivaId", out string? idStr) && int.TryParse(idStr, out int idVal))
        {
            return idVal;
        }
        return 1;
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerParosProgramacion(long programacionId)
    {
        var paros = await _context.TurnoParos
            .Where(p => p.ProgramacionId == programacionId && p.Activo)
            .Select(p => new
            {
                id = p.Id,
                tipoParo = p.TipoParo,
                duracionMinutos = p.DuracionMinutos,
                esProgramado = p.EsProgramado
            })
            .ToListAsync();

        return Json(paros);
    }

   [HttpPost]
public async Task<IActionResult> GuardarParosProgramacion([FromBody] GuardarParosDto dto)
{
    if (dto == null || dto.ProgramacionId <= 0)
        return Json(new { success = false, message = "Datos inválidos." });

    // 1. Obtener la programación para extraer su turno_id original
    var prog = await _context.Programaciones
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == dto.ProgramacionId);

    int? turnoIdAsociado = prog?.TurnoId;

    // 2. Limpiar paros previos de esta programación específica
    var parosActuales = await _context.TurnoParos
        .Where(p => p.ProgramacionId == dto.ProgramacionId)
        .ToListAsync();

    _context.TurnoParos.RemoveRange(parosActuales);

    // 3. Insertar los nuevos paros con turno_id y descripción
    if (dto.Paros != null && dto.Paros.Any())
    {
        foreach (var p in dto.Paros)
        {
            _context.TurnoParos.Add(new TurnoParo
            {
                ProgramacionId = dto.ProgramacionId,
                TurnoId = turnoIdAsociado, // <-- Asigna el turno_id automáticamente
                TipoParo = p.TipoParo,
                DuracionMinutos = p.DuracionMinutos,
                EsProgramado = p.EsProgramado,
                Descripcion = string.IsNullOrWhiteSpace(p.Descripcion) ? p.TipoParo : p.Descripcion, // <-- Guarda la descripción
                Activo = true
            });
        }
    }

    await _context.SaveChangesAsync();
    return Json(new { success = true });
}
}

public class GuardarParosDto
{
    public long ProgramacionId { get; set; }
    public List<ParoItemDto> Paros { get; set; } = new();
}

public class ParoItemDto
{
    public int Id { get; set; }
    public string TipoParo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int DuracionMinutos { get; set; }
    public bool EsProgramado { get; set; }
}