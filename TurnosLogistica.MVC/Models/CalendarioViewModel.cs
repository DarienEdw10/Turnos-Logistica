namespace TurnosLogistica.MVC.Models;

public class CalendarioViewModel
{
    public string Agrupacion { get; set; } = "proyecto";
    public string Granularidad { get; set; } = "mensual";
    public int PlantaId { get; set; } = 1;
    public DateTime FechaSeleccionada { get; set; } = DateTime.Today;
    public List<EventoCalendarioDto> Eventos { get; set; } = new();

    // Filtros activos
    public string? FiltroLinea { get; set; }
    public string? FiltroCelda { get; set; }
    public string? FiltroTurno { get; set; }

    // Listas para los dropdowns
    public List<string> LineasDisponibles { get; set; } = new();
    public List<string> CeldasDisponibles { get; set; } = new();
    public List<string> TurnosDisponibles { get; set; } = new(); // <--- AGREGAR ESTA LÍNEA
    public int MesActual => FechaSeleccionada.Month;
    public int AnioActual => FechaSeleccionada.Year;
    public string NombreMesAnio => FechaSeleccionada.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX"));

}

public class EventoCalendarioDto
{
    public long Id { get; set; }
    public DateTime Fecha { get; set; }
    public string ProyectoCodigo { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;
    public string CeldaCodigo { get; set; } = string.Empty;
    public string SapPartNumber { get; set; } = string.Empty;
    public string TurnoClave { get; set; } = "T1";
    public string Estado { get; set; } = "pendiente";
    public double Horas { get; set; } = 8.0;
    public int Cantidad { get; set; } = 0; // <--- Agrega esta propiedad
}