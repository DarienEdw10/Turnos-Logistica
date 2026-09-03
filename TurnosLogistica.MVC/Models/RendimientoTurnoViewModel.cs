namespace TurnosLogistica.MVC.Models;

public class RendimientoTurnoViewModel
{
    public DateTime Fecha { get; set; } = DateTime.Today;
    public int PlantaId { get; set; }
    public string? FiltroTurno { get; set; }
    public string? FiltroLinea { get; set; }

    // Listas para filtros
    public List<string> TurnosDisponibles { get; set; } = new();
    public List<string> LineasDisponibles { get; set; } = new();

    // Registros detallados por proyecto / celda
    public List<RendimientoItemViewModel> Items { get; set; } = new();

    // Tarjetas de resumen consolidado
    public double TotalHorasProgramadas => Math.Round(Items.Sum(i => i.HorasProgramadas), 2);
    public int TotalMinutosParoProg => Items.Sum(i => i.MinutosParoProgramados);
    public int TotalMinutosParoNoProg => Items.Sum(i => i.MinutosParoNoProgramados);
    public int TotalMinutosParos => TotalMinutosParoProg + TotalMinutosParoNoProg;
    public double TotalHorasParos => Math.Round(TotalMinutosParos / 60.0, 2);
    public double TotalHorasEfectivas => Math.Round(Items.Sum(i => i.HorasEfectivas), 2);

    public int TotalPiezasProgramadas => Items.Sum(i => i.PiezasProgramadas);
    public int TotalPiezasTerminadas => Items.Sum(i => i.PiezasTerminadas);
    public double EficienciaGeneralPct => TotalHorasProgramadas > 0 
        ? Math.Round((TotalHorasEfectivas / TotalHorasProgramadas) * 100.0, 1) 
        : 0;
}

public class RendimientoItemViewModel
{
    public long ProgramacionId { get; set; }
    public string TurnoClave { get; set; } = string.Empty;
    public string ProyectoCodigo { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;
    public string CeldaCodigo { get; set; } = string.Empty;
    public string SapPartNumber { get; set; } = string.Empty;

    public double HorasProgramadas { get; set; }
    public int MinutosParoProgramados { get; set; }
    public int MinutosParoNoProgramados { get; set; }
    public int TotalMinutosParo => MinutosParoProgramados + MinutosParoNoProgramados;
    public double HorasEfectivas => Math.Max(0, Math.Round(HorasProgramadas - (TotalMinutosParo / 60.0), 2));

    public int PiezasProgramadas { get; set; }
    public int PiezasTerminadas { get; set; }
    public double CumplimientoPiezasPct => PiezasProgramadas > 0 
        ? Math.Round(((double)PiezasTerminadas / PiezasProgramadas) * 100.0, 1) 
        : 0;
    public string Estatus { get; set; } = "Pendiente";
}