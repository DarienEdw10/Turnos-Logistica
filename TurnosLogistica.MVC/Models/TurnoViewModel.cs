namespace TurnosLogistica.MVC.Models;

public class TurnoViewModel
{
    public int Id { get; set; }
    public int PlantaId { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFin { get; set; }
    public double Horas { get; set; }
    public string ClaseColor { get; set; } = "matutino";
    public string? ColorHex { get; set; } = "#2563eb";
    public bool Activo { get; set; } = true;

    // Propiedades para la gestión de paros y tiempo neto
    public List<TurnoParoItemViewModel> Paros { get; set; } = new();

    public int? TotalMinutosParo => Paros?.Where(p => p.Activo).Sum(p => p.DuracionMinutos) ?? 0;

    public double? TiempoNetoHoras => Math.Max(0, Math.Round(Horas - ((TotalMinutosParo ?? 0) / 60.0), 2));
}

public class TurnoParoItemViewModel
{
    public int Id { get; set; }
    public int TurnoId { get; set; }
    public string TipoParo { get; set; } = string.Empty;
    public int DuracionMinutos { get; set; }
    public bool EsProgramado { get; set; } = true;
    public bool Activo { get; set; } = true;
}