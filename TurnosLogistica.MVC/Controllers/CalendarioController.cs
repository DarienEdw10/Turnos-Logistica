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
        string? fecha = null,
        int? mes = null,
        int? anio = null,
        string? linea = null,
        string? celda = null,
        string? turno = null)
    {
        int pId = plantaId ?? ObtenerPlantaActivaId();
        
        DateTime targetDate = DateTime.Today;
        if (!string.IsNullOrEmpty(fecha) && DateTime.TryParse(fecha, out var parsedDate))
        {
            targetDate = parsedDate;
        }
        else if (mes.HasValue && anio.HasValue)
        {
            targetDate = new DateTime(anio.Value, Math.Clamp(mes.Value, 1, 12), 1);
        }

        int m = targetDate.Month;
        int a = targetDate.Year;

        var vm = await _service.ObtenerCalendarioAsync(pId, agrupacion, granularidad, m, a, linea, celda, turno);
        
        if (vm != null)
        {
            vm.FechaSeleccionada = targetDate;
            vm.Granularidad = granularidad;
            vm.Agrupacion = agrupacion;
            vm.FiltroLinea = linea;
            vm.FiltroCelda = celda;
            vm.FiltroTurno = turno;
            vm.PlantaId = pId;
        }

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
                esProgramado = p.EsProgramado,
                categoriaParo = p.CategoriaParo // <-- NUEVO: Retorna la categoría (1, 2 o 3)
            })
            .ToListAsync();

        return Json(paros);
    }

    [HttpPost]
    public async Task<IActionResult> GuardarParosProgramacion([FromBody] GuardarParosDto dto)
    {
        if (dto == null || dto.ProgramacionId <= 0)
            return Json(new { success = false, message = "Datos inválidos." });

        var prog = await _context.Programaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProgramacionId);

        int? turnoIdAsociado = prog?.TurnoId;

        var parosActuales = await _context.TurnoParos
            .Where(p => p.ProgramacionId == dto.ProgramacionId)
            .ToListAsync();

        _context.TurnoParos.RemoveRange(parosActuales);

        if (dto.Paros != null && dto.Paros.Any())
        {
            foreach (var p in dto.Paros)
            {
                _context.TurnoParos.Add(new TurnoParo
                {
                    ProgramacionId = dto.ProgramacionId,
                    TurnoId = turnoIdAsociado,
                    TipoParo = p.TipoParo,
                    Descripcion = string.IsNullOrWhiteSpace(p.Descripcion) ? p.TipoParo : p.Descripcion,
                    DuracionMinutos = p.DuracionMinutos,
                    EsProgramado = p.EsProgramado,
                    CategoriaParo = p.CategoriaParo > 0 ? p.CategoriaParo : (p.EsProgramado ? (byte)1 : (byte)3), // <-- Guarda la categoría seleccionada (1, 2 o 3)
                    Activo = true
                });
            }
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
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
        public byte CategoriaParo { get; set; } // <-- NUEVO: Recibe 1 (Base), 2 (Temporal) o 3 (Imprevisto)
    }
}