namespace TurnosLogistica.MVC.Models;

public class TurnoViewModel
{
    public int Id { get; set; }
    public int PlantaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFin { get; set; }
    public double Horas { get; set; }
    public string Clave { get; set; } = "T1";
    public string ClaseColor { get; set; } = "t1";
    public string ColorHex { get; set; } = "#2563eb";
    public bool Activo { get; set; } = true;
}