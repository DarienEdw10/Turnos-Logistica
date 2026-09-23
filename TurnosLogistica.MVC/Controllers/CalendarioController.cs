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
        var prog = await _context.Programaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == programacionId);

        if (prog == null)
            return Json(new List<object>());

        // Traemos tanto los paros específicos de esta programación (temporales y fallas) 
        // como los paros base generales configurados para el turno de esta orden (TurnoId sin ProgramacionId)
        var paros = await _context.TurnoParos
            .AsNoTracking()
            .Where(p => p.Activo && (
                p.ProgramacionId == programacionId || 
                (p.TurnoId == prog.TurnoId && p.ProgramacionId == null)
            ))
            .Select(p => new
            {
                id = p.Id,
                tipoParo = p.TipoParo,
                descripcion = p.Descripcion ?? p.TipoParo,
                duracionMinutos = p.DuracionMinutos,
                categoriaParo = p.CategoriaParo > 0 ? p.CategoriaParo : (p.ProgramacionId == null ? (byte)1 : (byte)3)
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

        // Traemos los paros que ya existían previamente en la base de datos para esta programación
        var parosExistentes = await _context.TurnoParos
            .Where(p => p.ProgramacionId == dto.ProgramacionId && p.Activo)
            .ToListAsync();

        if (dto.Paros != null && dto.Paros.Any())
        {
            foreach (var p in dto.Paros)
            {
                if (p.Id > 0)
                {
                    var paroDb = parosExistentes.FirstOrDefault(x => x.Id == p.Id);
                    if (paroDb != null)
                    {
                        // Mantenemos sus valores históricos intactos
                        continue; 
                    }
                }
                else
                {
                    // Si el ID es 0, significa que es una incidencia NUEVA que el usuario acaba de agregar en el modal
                    byte cat = p.CategoriaParo > 0 ? p.CategoriaParo : (byte)3;
                    bool esProgReal = (cat == 1 || cat == 2);

                    string tipoParoTexto = cat switch
                    {
                        1 => "Base / Turno",
                        2 => "Temporal (Día)",
                        3 => "No Programado (Falla)",
                        _ => "No Programado (Falla)"
                    };

                    _context.TurnoParos.Add(new TurnoParo
                    {
                        ProgramacionId = dto.ProgramacionId,
                        TurnoId = turnoIdAsociado,
                        TipoParo = tipoParoTexto,
                        Descripcion = string.IsNullOrWhiteSpace(p.TipoParo) ? tipoParoTexto : p.TipoParo,
                        DuracionMinutos = p.DuracionMinutos,
                        EsProgramado = esProgReal,
                        CategoriaParo = cat,
                        Activo = true
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTodasCeldasPlanta()
    {
        int pId = ObtenerPlantaActivaId();
        
        var celdas = await (from c in _context.Celdas
                            join l in _context.Lineas on c.LineaId equals l.Id
                            where c.Activa && l.PlantaId == pId && l.Activa
                            orderby c.Codigo
                            select new { id = c.Id, texto = c.Codigo })
                           .Distinct()
                           .ToListAsync();

        return Json(celdas);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCeldasPorLineaNombre(string lineaNombre)
    {
        if (string.IsNullOrWhiteSpace(lineaNombre) || lineaNombre == "Todas")
        {
            return await ObtenerTodasCeldasPlanta();
        }

        int pId = ObtenerPlantaActivaId();
        bool esIdNumerico = int.TryParse(lineaNombre, out int lineaIdParsed);

        var query = from c in _context.Celdas
                    join l in _context.Lineas on c.LineaId equals l.Id
                    where c.Activa && l.PlantaId == pId && l.Activa
                    select new { Celda = c, Linea = l };

        if (esIdNumerico)
        {
            query = query.Where(x => x.Celda.LineaId == lineaIdParsed);
        }
        else
        {
            string termino = lineaNombre.Trim().ToLower();
            query = query.Where(x => x.Linea.Nombre.ToLower().Contains(termino) || x.Linea.Codigo.ToLower().Contains(termino));
        }

        var celdas = await query
            .OrderBy(x => x.Celda.Codigo)
            .Select(x => new { id = x.Celda.Id, texto = x.Celda.Codigo })
            .Distinct()
            .ToListAsync();

        return Json(celdas);
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
        public byte CategoriaParo { get; set; }
    }
}