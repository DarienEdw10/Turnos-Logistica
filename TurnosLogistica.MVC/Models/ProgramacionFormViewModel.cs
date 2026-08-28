using System.ComponentModel.DataAnnotations;

namespace TurnosLogistica.MVC.Models;

public class ProgramacionFormViewModel
{
    [Required]
    public int ProyectoId { get; set; }

    [Required]
    public int LineaId { get; set; }

    [Required]
    public int CeldaId { get; set; }

    public int? EstacionId { get; set; }

    [Required]
    public int NumeroParteId { get; set; }

    [Required]
    public DateTime FechaProduccion { get; set; } = DateTime.Today;

    [Required]
    public int TurnoId { get; set; } = 1; // 1 = T1, 2 = T2, 3 = T3

    public double TiempoEstimadoHoras { get; set; } = 8.0;

    [Required(ErrorMessage = "La razón es obligatoria")]
    public string RazonObligatoria { get; set; } = string.Empty;

    public string Estado { get; set; } = "pendiente";
}