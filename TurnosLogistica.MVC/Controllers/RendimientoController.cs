using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Controllers;

public class RendimientoController : Controller
{
    private readonly AppDbContext _context;

    public RendimientoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fecha, string? turno, string? linea)
    {
        var vm = await ConsultarRendimientoAsync(fecha, turno, linea);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCsv(DateTime? fecha, string? turno, string? linea)
    {
        var vm = await ConsultarRendimientoAsync(fecha, turno, linea);
        var sb = new StringBuilder();

        // Encabezados CSV (compatible con Excel mediante BOM UTF-8)
        sb.AppendLine("Turno;Proyecto;Linea;Celda;No. Parte;Estatus;Horas Prog;Paro Prog (min);Paro Falla (min);Total Paro (min);Horas Efectivas;Pzas Prog;Pzas Term;Cumplimiento (%)");

        foreach (var i in vm.Items)
        {
            sb.AppendLine($"{i.TurnoClave};{i.ProyectoCodigo};{i.LineaNombre};{i.CeldaCodigo};{i.SapPartNumber};{i.Estatus};{i.HorasProgramadas:N2};{i.MinutosParoProgramados};{i.MinutosParoNoProgramados};{i.TotalMinutosParo};{i.HorasEfectivas:N2};{i.PiezasProgramadas};{i.PiezasTerminadas};{i.CumplimientoPiezasPct}%");
        }

        byte[] buffer = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        string nombreArchivo = $"Rendimiento_Produccion_{vm.Fecha:yyyyMMdd}.csv";

        return File(buffer, "text/csv; charset=utf-8", nombreArchivo);
    }

    private async Task<RendimientoTurnoViewModel> ConsultarRendimientoAsync(DateTime? fecha, string? turno, string? linea)
    {
        DateTime fechaFiltro = fecha ?? DateTime.Today;
        int plantaId = ObtenerPlantaActivaId();

        // Joins directos basados en los modelos de dominio
        var query = from p in _context.Programaciones.AsNoTracking()
                    join t in _context.Turnos.AsNoTracking() on p.TurnoId equals t.Id
                    join np in _context.NumerosDeParte.AsNoTracking() on p.NumeroParteId equals np.Id
                    join l in _context.Lineas.AsNoTracking() on np.LineaId equals l.Id
                    join c in _context.Celdas.AsNoTracking() on np.CeldaId equals c.Id into gjCelda
                    from subCelda in gjCelda.DefaultIfEmpty()
                    join pr in _context.Proyectos.AsNoTracking() on l.ProyectoId equals pr.Id into gjProyecto
                    from subProyecto in gjProyecto.DefaultIfEmpty()
                    where p.Fecha.Date == fechaFiltro.Date && l.PlantaId == plantaId
                    select new
                    {
                        p.Id,
                        p.HorasProgramadas,
                        p.CantidadProgramada,
                        p.PiezasTerminadas,
                        p.Estatus,
                        TurnoNombre = t.Nombre,
                        ProyectoCodigo = subProyecto != null ? subProyecto.Codigo : "S/P",
                        LineaNombre = l.Nombre,
                        CeldaCodigo = subCelda != null ? subCelda.Codigo : "N/A",
                        SapPartNumber = np.SapPartNumber
                    };

        if (!string.IsNullOrEmpty(turno) && turno != "Todos")
            query = query.Where(x => x.TurnoNombre == turno);

        if (!string.IsNullOrEmpty(linea) && linea != "Todas")
            query = query.Where(x => x.LineaNombre == linea);

        var resultados = await query.ToListAsync();
        var progIds = resultados.Select(r => r.Id).ToList();

        // Consultar los paros registrados para estas programaciones
        var paros = await _context.TurnoParos
            .AsNoTracking()
            .Where(tp => tp.ProgramacionId.HasValue && progIds.Contains(tp.ProgramacionId.Value) && tp.Activo)
            .ToListAsync();

        var vm = new RendimientoTurnoViewModel
        {
            Fecha = fechaFiltro,
            PlantaId = plantaId,
            FiltroTurno = turno,
            FiltroLinea = linea,
            TurnosDisponibles = await _context.Turnos
                .Where(t => t.PlantaId == plantaId && t.Activo)
                .Select(t => t.Nombre)
                .Distinct()
                .ToListAsync(),
            LineasDisponibles = await _context.Lineas
                .Where(l => l.PlantaId == plantaId && l.Activa)
                .Select(l => l.Nombre)
                .Distinct()
                .ToListAsync()
        };

        foreach (var item in resultados)
        {
            var parosProg = paros.Where(tp => tp.ProgramacionId == item.Id).ToList();
            int minProg = parosProg.Where(tp => tp.EsProgramado).Sum(tp => tp.DuracionMinutos);
            int minNoProg = parosProg.Where(tp => !tp.EsProgramado).Sum(tp => tp.DuracionMinutos);

            vm.Items.Add(new RendimientoItemViewModel
            {
                ProgramacionId = item.Id,
                TurnoClave = item.TurnoNombre,
                ProyectoCodigo = item.ProyectoCodigo,
                LineaNombre = item.LineaNombre,
                CeldaCodigo = item.CeldaCodigo,
                SapPartNumber = item.SapPartNumber,
                HorasProgramadas = (double)item.HorasProgramadas,
                MinutosParoProgramados = minProg,
                MinutosParoNoProgramados = minNoProg,
                PiezasProgramadas = item.CantidadProgramada,
                PiezasTerminadas = item.PiezasTerminadas,
                Estatus = item.Estatus
            });
        }

        return vm;
    }

    [HttpPost]
    public async Task<IActionResult> ActualizarCierre([FromBody] ActualizarCierreDto dto)
    {
        if (dto == null || dto.ProgramacionId <= 0)
            return Json(new { success = false, message = "Datos inválidos." });

        try
        {
            var prog = await _context.Programaciones.FirstOrDefaultAsync(p => p.Id == dto.ProgramacionId);
            if (prog == null)
                return Json(new { success = false, message = "Registro de programación no encontrado." });

            prog.Estatus = dto.Estatus;
            prog.PiezasTerminadas = dto.PiezasTerminadas;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            string detalle = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return Json(new { success = false, message = "Error al persistir en base de datos: " + detalle });
        }
    }

    private int ObtenerPlantaActivaId()
    {
        if (Request.Cookies.TryGetValue("PlantaActivaId", out string? idStr) && int.TryParse(idStr, out int idVal))
            return idVal;
        return 1;
    }
}

public class ActualizarCierreDto
{
    public long ProgramacionId { get; set; }
    public string Estatus { get; set; } = "pendiente";
    public int PiezasTerminadas { get; set; }
}