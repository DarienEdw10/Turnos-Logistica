using TurnosLogistica.Domain.Models;

namespace TurnosLogistica.MVC.Models;

public class AuditoriaViewModel
{
    // Propiedades de rango y filtros
    public DateTime? FechaDesde { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime? FechaHasta { get; set; } = DateTime.Today;

    public DateTime? FechaInicio 
    { 
        get => FechaDesde; 
        set => FechaDesde = value; 
    }

    public DateTime? FechaFin 
    { 
        get => FechaHasta; 
        set => FechaHasta = value; 
    }

    public string? FiltroAccion { get; set; }
    public string? FiltroUsuario { get; set; }
    public string? ErrorMensaje { get; set; }

    public List<RegistroAuditoriaDto> Registros { get; set; } = new();
}