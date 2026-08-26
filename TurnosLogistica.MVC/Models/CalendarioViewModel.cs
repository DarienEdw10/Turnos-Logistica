namespace TurnosLogistica.MVC.Models;

public class CalendarioViewModel
{
    public string Agrupacion { get; set; } = "proyecto"; // proyecto, linea, celda, parte
    public string Granularidad { get; set; } = "mensual"; // mensual, semanal, diario
    public int PlantaId { get; set; } = 1;
    public string PlantaNombre { get; set; } = "Planta 1 — Cuautitlán";
    public DateTime FechaSeleccionada { get; set; } = DateTime.Today;
    public List<EventoCalendarioDto> Eventos { get; set; } = new();
}

public class EventoCalendarioDto
{
    public long Id { get; set; }
    public string ProyectoCodigo { get; set; } = "";
    public string LineaNombre { get; set; } = "";
    public string CeldaCodigo { get; set; } = "";
    public string SapPartNumber { get; set; } = "";
    public string TurnoClave { get; set; } = "T1"; // T1, T2, T3
    public string Estado { get; set; } = "Programado"; // Programado, En curso, Terminado, Cancelado
    public double Horas { get; set; } = 8.0;
    public DateTime Fecha { get; set; }
}