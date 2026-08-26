namespace TurnosLogistica.MVC.Models;

public class TurnoViewModel
{
    public int Id { get; set; }
    public string Clave { get; set; } = "T1";
    public string Nombre { get; set; } = "Matutino";
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFin { get; set; }
    public double FactorTiempoHoras { get; set; }
    public string ColorHex { get; set; } = "#2563eb";
    public bool Activo { get; set; } = true;
}