namespace TurnosLogistica.MVC.Models;

public class ParoTemporalCatalogoViewModel
{
    public long Id { get; set; }
    public string TipoParo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int DuracionMinutos { get; set; } = 30;
    public bool Activo { get; set; } = true;
}