using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Models;
using TurnoDomain = TurnosLogistica.Domain.Models.Turno;
namespace TurnosLogistica.MVC.Services;

public interface IPlanificacionService
{
    // Catálogos y Cascada
    Task<IEnumerable<Planta>> ObtenerPlantasActivasAsync();
    Task<IEnumerable<Proyecto>> ObtenerProyectosPorPlantaAsync(int plantaId);
    Task<IEnumerable<Linea>> ObtenerLineasPorProyectoAsync(int proyectoId);
    Task<IEnumerable<Celda>> ObtenerCeldasPorLineaAsync(int lineaId);
    Task<IEnumerable<Estacion>> ObtenerEstacionesPorCeldaAsync(int celdaId);
    Task<IEnumerable<NumeroDeParte>> ObtenerPartesPorCeldaAsync(int celdaId);
    Task<IEnumerable<Turno>> ObtenerTurnosPorPlantaAsync(int plantaId);

    // Calendario y Programación
    Task<CalendarioViewModel> ObtenerCalendarioAsync(int plantaId, string agrupacion, string granularidad, int mes, int anio, string? filtroLinea = null, string? filtroCelda = null, string? filtroTurno = null);
    Task<bool> GuardarProgramacionAsync(ProgramacionFormViewModel model, int usuarioId);

    // Auditoría y Partes
    Task<IEnumerable<NumeroDeParte>> ObtenerMaestroPartesAsync(int plantaId);
    Task<AuditoriaViewModel> ObtenerHistorialAuditoriaAsync(DateTime? desde, DateTime? hasta, string? accion, string? usuario);
}