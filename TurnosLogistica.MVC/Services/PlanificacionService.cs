using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.Domain.Repositories;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Services;

public class PlanificacionService : IPlanificacionService
{
    private readonly AppDbContext _context;
    private readonly IRepository<Planta> _plantaRepo;
    private readonly IRepository<Proyecto> _proyectoRepo;
    private readonly IRepository<Linea> _lineaRepo;
    private readonly IRepository<Celda> _celdaRepo;
    private readonly IRepository<Estacion> _estacionRepo;
    private readonly IRepository<NumeroDeParte> _parteRepo;
    private readonly IRepository<Turno> _turnoRepo;
    private readonly IRepository<ProgramacionProduccion> _progRepo;
    private readonly IRepository<HistorialAgenda> _historialRepo;

    public PlanificacionService(
        AppDbContext context,
        IRepository<Planta> plantaRepo,
        IRepository<Proyecto> proyectoRepo,
        IRepository<Linea> lineaRepo,
        IRepository<Celda> celdaRepo,
        IRepository<Estacion> estacionRepo,
        IRepository<NumeroDeParte> parteRepo,
        IRepository<Turno> turnoRepo,
        IRepository<ProgramacionProduccion> progRepo,
        IRepository<HistorialAgenda> historialRepo)
    {
        _context = context;
        _plantaRepo = plantaRepo;
        _proyectoRepo = proyectoRepo;
        _lineaRepo = lineaRepo;
        _celdaRepo = celdaRepo;
        _estacionRepo = estacionRepo;
        _parteRepo = parteRepo;
        _turnoRepo = turnoRepo;
        _progRepo = progRepo;
        _historialRepo = historialRepo;
    }

    public async Task<IEnumerable<Planta>> ObtenerPlantasActivasAsync() =>
        await _plantaRepo.FindAsync(p => p.Activa);

    public async Task<IEnumerable<Proyecto>> ObtenerProyectosPorPlantaAsync(int plantaId) =>
        await _proyectoRepo.FindAsync(p => p.PlantaId == plantaId && p.Activo);

    public async Task<IEnumerable<Linea>> ObtenerLineasPorProyectoAsync(int proyectoId) =>
        await _lineaRepo.FindAsync(l => l.ProyectoId == proyectoId && l.Activa);

    public async Task<IEnumerable<Celda>> ObtenerCeldasPorLineaAsync(int lineaId) =>
        await _celdaRepo.FindAsync(c => c.LineaId == lineaId && c.Activa);

    public async Task<IEnumerable<Estacion>> ObtenerEstacionesPorCeldaAsync(int celdaId) =>
        await _estacionRepo.FindAsync(e => e.CeldaId == celdaId && e.Activa);

    public async Task<IEnumerable<NumeroDeParte>> ObtenerPartesPorCeldaAsync(int celdaId) =>
        await _parteRepo.FindAsync(p => p.CeldaId == celdaId && p.Activo);

    public async Task<IEnumerable<Turno>> ObtenerTurnosPorPlantaAsync(int plantaId) =>
        await _turnoRepo.FindAsync(t => t.PlantaId == plantaId && t.Activo);

    public async Task<IEnumerable<NumeroDeParte>> ObtenerMaestroPartesAsync(int plantaId) =>
        await _parteRepo.FindAsync(p => p.PlantaId == plantaId && p.Activo);

    public async Task<CalendarioViewModel> ObtenerCalendarioAsync(
        int plantaId,
        string agrupacion,
        string granularidad,
        int mes,
        int anio,
        string? filtroLinea = null,
        string? filtroCelda = null,
        string? filtroTurno = null)
    {
        // 1. Determinar el rango de fechas según granularidad
        DateTime fechaBase = new DateTime(anio, mes, 1);
        DateTime fechaInicio;
        DateTime fechaFin;

        if (granularidad == "diario")
        {
            fechaInicio = DateTime.Today;
            fechaFin = fechaInicio.AddDays(1);
        }
        else if (granularidad == "semanal")
        {
            // Obtener el lunes de la semana actual del mes
            int delta = DayOfWeek.Monday - DateTime.Today.DayOfWeek;
            if (delta > 0) delta -= 7;
            fechaInicio = DateTime.Today.AddDays(delta);
            fechaFin = fechaInicio.AddDays(7);
        }
        else // mensual
        {
            fechaInicio = new DateTime(anio, mes, 1);
            fechaFin = fechaInicio.AddMonths(1);
        }

        // 2. Consulta proyectada a BD
        var rawQuery = from prog in _context.Programaciones
                       join parte in _context.NumerosDeParte on prog.NumeroParteId equals parte.Id
                       join turno in _context.Turnos on prog.TurnoId equals turno.Id
                       join celda in _context.Celdas on parte.CeldaId equals (int?)celda.Id into cJ
                       from celda in cJ.DefaultIfEmpty()
                       join linea in _context.Lineas on parte.LineaId equals linea.Id into lJ
                       from linea in lJ.DefaultIfEmpty()
                       join pry in _context.Proyectos on linea.ProyectoId equals (int?)pry.Id into pJ
                       from pry in pJ.DefaultIfEmpty()
                       where prog.Fecha >= fechaInicio && prog.Fecha < fechaFin
                          && parte.PlantaId == plantaId
                       select new
                       {
                           prog.Id,
                           prog.Fecha,
                           ProyectoCodigo = pry != null ? pry.Codigo : (parte.FinalAssembly ?? "PROYECTO"),
                           LineaNombre = linea != null ? linea.Codigo : "L.A-1",
                           CeldaCodigo = celda != null ? celda.Codigo : "C-101",
                           parte.SapPartNumber,
                           TurnoNombre = turno.Nombre,
                           TurnoHoraInicio = turno.HoraInicio,
                           TurnoHoraFin = turno.HoraFin,
                           Estado = prog.Estatus,
                           Cantidad = prog.CantidadProgramada
                       };

        // Filtros
        if (!string.IsNullOrEmpty(filtroLinea) && filtroLinea != "Todas")
            rawQuery = rawQuery.Where(e => e.LineaNombre == filtroLinea);

        if (!string.IsNullOrEmpty(filtroCelda) && filtroCelda != "Todas")
            rawQuery = rawQuery.Where(e => e.CeldaCodigo == filtroCelda);

        if (!string.IsNullOrEmpty(filtroTurno) && filtroTurno != "Todos")
            rawQuery = rawQuery.Where(e => e.TurnoNombre == filtroTurno);

        var data = await rawQuery.ToListAsync();

        // 3. Mapeo en memoria y consolidación según la agrupación solicitada
        List<EventoCalendarioDto> eventos;

        if (agrupacion == "proyecto")
        {
            // Agrupa por Fecha, Turno y Proyecto sumando horas y piezas
            eventos = data.GroupBy(d => new { d.Fecha, d.TurnoNombre, d.ProyectoCodigo })
                .Select(g =>
                {
                    var primerItem = g.First();
                    double duracion = primerItem.TurnoHoraFin <= primerItem.TurnoHoraInicio
                        ? (primerItem.TurnoHoraFin.Add(TimeSpan.FromDays(1)) - primerItem.TurnoHoraInicio).TotalHours
                        : (primerItem.TurnoHoraFin - primerItem.TurnoHoraInicio).TotalHours;

                    return new EventoCalendarioDto
                    {
                        Id = primerItem.Id,
                        Fecha = g.Key.Fecha,
                        ProyectoCodigo = g.Key.ProyectoCodigo,
                        LineaNombre = $"{g.Select(x => x.LineaNombre).Distinct().Count()} Líneas",
                        CeldaCodigo = $"{g.Select(x => x.CeldaCodigo).Distinct().Count()} Celdas",
                        SapPartNumber = $"{g.Count()} Partes",
                        TurnoClave = g.Key.TurnoNombre,
                        Estado = primerItem.Estado,
                        Horas = Math.Round(duracion, 1),
                        Cantidad = g.Sum(x => x.Cantidad)
                    };
                }).ToList();
        }
        else if (agrupacion == "linea")
        {
            // Agrupa por Fecha, Turno y Línea
            eventos = data.GroupBy(d => new { d.Fecha, d.TurnoNombre, d.ProyectoCodigo, d.LineaNombre })
                .Select(g =>
                {
                    var primerItem = g.First();
                    double duracion = primerItem.TurnoHoraFin <= primerItem.TurnoHoraInicio
                        ? (primerItem.TurnoHoraFin.Add(TimeSpan.FromDays(1)) - primerItem.TurnoHoraInicio).TotalHours
                        : (primerItem.TurnoHoraFin - primerItem.TurnoHoraInicio).TotalHours;

                    return new EventoCalendarioDto
                    {
                        Id = primerItem.Id,
                        Fecha = g.Key.Fecha,
                        ProyectoCodigo = g.Key.ProyectoCodigo,
                        LineaNombre = g.Key.LineaNombre,
                        CeldaCodigo = $"{g.Select(x => x.CeldaCodigo).Distinct().Count()} Celdas",
                        SapPartNumber = $"{g.Count()} Partes",
                        TurnoClave = g.Key.TurnoNombre,
                        Estado = primerItem.Estado,
                        Horas = Math.Round(duracion, 1),
                        Cantidad = g.Sum(x => x.Cantidad)
                    };
                }).ToList();
        }
        else if (agrupacion == "celda")
        {
            // Agrupa por Fecha, Turno y Celda
            eventos = data.GroupBy(d => new { d.Fecha, d.TurnoNombre, d.ProyectoCodigo, d.LineaNombre, d.CeldaCodigo })
                .Select(g =>
                {
                    var primerItem = g.First();
                    double duracion = primerItem.TurnoHoraFin <= primerItem.TurnoHoraInicio
                        ? (primerItem.TurnoHoraFin.Add(TimeSpan.FromDays(1)) - primerItem.TurnoHoraInicio).TotalHours
                        : (primerItem.TurnoHoraFin - primerItem.TurnoHoraInicio).TotalHours;

                    return new EventoCalendarioDto
                    {
                        Id = primerItem.Id,
                        Fecha = g.Key.Fecha,
                        ProyectoCodigo = g.Key.ProyectoCodigo,
                        LineaNombre = g.Key.LineaNombre,
                        CeldaCodigo = g.Key.CeldaCodigo,
                        SapPartNumber = $"{g.Count()} Partes",
                        TurnoClave = g.Key.TurnoNombre,
                        Estado = primerItem.Estado,
                        Horas = Math.Round(duracion, 1),
                        Cantidad = g.Sum(x => x.Cantidad)
                    };
                }).ToList();
        }
        else // "parte" -> Máximo nivel de detalle (1 tarjeta por número de parte)
        {
            eventos = data.Select(d =>
            {
                double duracion = d.TurnoHoraFin <= d.TurnoHoraInicio
                    ? (d.TurnoHoraFin.Add(TimeSpan.FromDays(1)) - d.TurnoHoraInicio).TotalHours
                    : (d.TurnoHoraFin - d.TurnoHoraInicio).TotalHours;

                return new EventoCalendarioDto
                {
                    Id = d.Id,
                    Fecha = d.Fecha,
                    ProyectoCodigo = d.ProyectoCodigo,
                    LineaNombre = d.LineaNombre,
                    CeldaCodigo = d.CeldaCodigo,
                    SapPartNumber = d.SapPartNumber,
                    TurnoClave = d.TurnoNombre,
                    Estado = d.Estado,
                    Horas = Math.Round(duracion, 1),
                    Cantidad = d.Cantidad
                };
            }).ToList();
        }

        var lineasDisponibles = await _context.Lineas
            .Where(l => l.PlantaId == plantaId && l.Activa)
            .Select(l => l.Codigo)
            .Distinct()
            .ToListAsync();

        var celdasDisponibles = await (from c in _context.Celdas
                                       join l in _context.Lineas on c.LineaId equals l.Id
                                       where l.PlantaId == plantaId && c.Activa
                                       select c.Codigo).Distinct().ToListAsync();

        var turnosDisponibles = await _context.Turnos
            .Where(t => t.PlantaId == plantaId && t.Activo)
            .Select(t => t.Nombre)
            .Distinct()
            .ToListAsync();

        return new CalendarioViewModel
        {
            Agrupacion = agrupacion,
            Granularidad = granularidad,
            PlantaId = plantaId,
            FechaSeleccionada = fechaInicio,
            FiltroLinea = filtroLinea,
            FiltroCelda = filtroCelda,
            FiltroTurno = filtroTurno,
            LineasDisponibles = lineasDisponibles,
            CeldasDisponibles = celdasDisponibles,
            TurnosDisponibles = turnosDisponibles,
            Eventos = eventos
        };
    }

    public async Task<bool> GuardarProgramacionAsync(ProgramacionFormViewModel model, int usuarioId)
    {
        int parteId = model.NumeroParteId > 0 ? model.NumeroParteId : 1;

        // Si el usuario especificó la cantidad, se usa esa; de lo contrario se calcula con base en JPH
        int cantidadFinal = model.CantidadProgramada;
        if (cantidadFinal <= 0)
        {
            var parte = await _parteRepo.GetByIdAsync(parteId);
            decimal jph = parte?.JPH ?? 50m;
            decimal oa = (parte?.OA ?? 100m) / 100m;
            decimal horas = (decimal)model.TiempoEstimadoHoras;
            cantidadFinal = (int)Math.Round(horas * jph * oa);
            if (cantidadFinal <= 0) cantidadFinal = 400; // Fallback de seguridad
        }

        var nuevaProg = new ProgramacionProduccion
        {
            NumeroParteId = parteId,
            Fecha = model.FechaProduccion.Date,
            TurnoId = model.TurnoId,
            CantidadProgramada = cantidadFinal, // <-- ¡Valor dinámico real sin hardcode!
            OrdenProducir = 1,
            VentanasSalida = 4,
            Estatus = "pendiente",
            RazonCambio = model.RazonObligatoria,
            CreadoPor = usuarioId,
            CreadoAt = DateTime.UtcNow
        };

        await _progRepo.AddAsync(nuevaProg);
        await _progRepo.SaveChangesAsync();

        var jsonPayload = JsonSerializer.Serialize(new
        {
            programacion_id = nuevaProg.Id,
            fecha = nuevaProg.Fecha.ToString("yyyy-MM-dd"),
            turno_id = nuevaProg.TurnoId,
            numero_parte_id = nuevaProg.NumeroParteId,
            horas = model.TiempoEstimadoHoras,
            cantidad = nuevaProg.CantidadProgramada,
            estatus = nuevaProg.Estatus
        });

        var historial = new HistorialAgenda
        {
            ProgramacionId = nuevaProg.Id,
            Accion = "CREACION",
            ValorAnterior = null,
            ValorNuevo = jsonPayload,
            Razon = model.RazonObligatoria,
            UsuarioId = usuarioId,
            FechaAccion = DateTime.UtcNow
        };

        await _historialRepo.AddAsync(historial);
        return await _historialRepo.SaveChangesAsync() > 0;
    }

    public async Task<AuditoriaViewModel> ObtenerHistorialAuditoriaAsync(DateTime? desde, DateTime? hasta, string? accion, string? usuario)
    {
        var historial = await _historialRepo.GetAllAsync();

        if (desde.HasValue) historial = historial.Where(h => h.FechaAccion >= desde.Value);
        if (hasta.HasValue) historial = historial.Where(h => h.FechaAccion <= hasta.Value.AddDays(1));
        if (!string.IsNullOrEmpty(accion) && accion != "Todas") historial = historial.Where(h => h.Accion == accion);

        var registros = historial.Select(h => new RegistroAuditoriaDto
        {
            Id = h.Id,
            FechaHora = h.FechaAccion,
            Usuario = "Mtro. García",
            Rol = "Jefe Logística",
            Accion = h.Accion,
            DescripcionCambio = h.ValorNuevo,
            RazonObligatoria = h.Razon
        }).OrderByDescending(r => r.FechaHora).ToList();

        return new AuditoriaViewModel
        {
            FechaDesde = desde,
            FechaHasta = hasta,
            FiltroAccion = accion,
            FiltroUsuario = usuario,
            Registros = registros
        };
    }
}