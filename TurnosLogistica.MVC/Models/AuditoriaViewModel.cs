namespace TurnosLogistica.MVC.Models;

public class AuditoriaViewModel
{
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? FiltroAccion { get; set; }
    public string? FiltroUsuario { get; set; }
    public List<RegistroAuditoriaDto> Registros { get; set; } = new();
}

public class RegistroAuditoriaDto
{
    public long Id { get; set; }
    public DateTime FechaHora { get; set; }
    public string Usuario { get; set; } = "";
    public string Rol { get; set; } = "";
    public string Accion { get; set; } = ""; // Alta, Modificacion, Cancelacion, Reprogramacion
    public string DescripcionCambio { get; set; } = "";
    public string RazonObligatoria { get; set; } = "";
}