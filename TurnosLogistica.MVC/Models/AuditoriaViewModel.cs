namespace TurnosLogistica.MVC.Models;

public class AuditoriaViewModel
{
    // Propiedades de rango y filtros (anulables para compatibilidad completa con el servicio)
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

public class RegistroAuditoriaDto
{
    public long Id { get; set; }
    public long HistorialId { get => Id; set => Id = value; }

    public DateTime FechaHora { get; set; }
    public DateTime FechaAccion { get => FechaHora; set => FechaHora = value; }

    public string Usuario { get; set; } = string.Empty;
    public string UsuarioResponsable { get => Usuario; set => Usuario = value; }

    public string Rol { get; set; } = string.Empty;
    public string RolUsuario { get => Rol; set => Rol = value; }

    public string Accion { get; set; } = string.Empty;
    public string AccionBadgeClase { get; set; } = "info";

    public string DescripcionCambio { get; set; } = string.Empty;
    public string AgendaDetalle { get => DescripcionCambio; set => DescripcionCambio = value; }

    public string RazonObligatoria { get; set; } = string.Empty;
    public string Razon { get => RazonObligatoria; set => RazonObligatoria = value; }

    // Metadatos auxiliares de pieza y turno
    public string SapPartNumber { get; set; } = string.Empty;
    public string NoDeParte { get; set; } = string.Empty;
    public DateTime FechaProgramada { get; set; }
    public string Turno { get; set; } = string.Empty;
}