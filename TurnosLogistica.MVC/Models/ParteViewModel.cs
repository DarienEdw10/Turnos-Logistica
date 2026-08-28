namespace TurnosLogistica.MVC.Models;

public class ParteItemDto
{
    public int Id { get; set; }
    public string SapPartNumber { get; set; } = string.Empty;
    public string NoDeParte { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? FinalAssembly { get; set; }
    public string? Familia { get; set; }
    public decimal OA { get; set; }
    public int JPH { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string CeldaCodigo { get; set; } = string.Empty;
    public string? ImagenAyudaVisual { get; set; }
    public bool Activo { get; set; }
}