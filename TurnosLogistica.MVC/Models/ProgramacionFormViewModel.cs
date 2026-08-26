namespace TurnosLogistica.MVC.Models;

public class ProgramacionFormViewModel
{
    public int PlantaId { get; set; } = 1;
    public int ProyectoId { get; set; }
    public int LineaId { get; set; }
    public int CeldaId { get; set; }
    public int EstacionId { get; set; }
    public DateTime FechaProduccion { get; set; } = DateTime.Today;
    public List<string> SapPartNumbersSeleccionados { get; set; } = new();
    public string Turno { get; set; } = "t1"; // t1, t2, t3
    public double TiempoEstimadoHoras { get; set; } = 8.0;
    public string Estado { get; set; } = "Programado";
    public string RazonObligatoria { get; set; } = "";
}