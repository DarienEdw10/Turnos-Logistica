namespace TurnosLogistica.MVC.Models;

public class ParteViewModel
{
    public int Id { get; set; }
    public string SapPartNumber { get; set; } = "";
    public string NoDeParte { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string? FinalAssembly { get; set; }
    public string? Familia { get; set; }
    public decimal OA { get; set; }
    public int JPH { get; set; }
    public string LineaNombre { get; set; } = "";
    public string CeldaNombre { get; set; } = "";
    public string? EstacionNombre { get; set; }
    public string? ImagenAyudaVisualUrl { get; set; }
    public bool Activo { get; set; } = true;
}