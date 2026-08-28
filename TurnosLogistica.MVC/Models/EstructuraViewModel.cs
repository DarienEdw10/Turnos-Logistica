namespace TurnosLogistica.MVC.Models;

public class EstructuraViewModel
{
    public int PlantaId { get; set; }
    public string PlantaNombre { get; set; } = string.Empty;
    public List<ProyectoNodoDto> Proyectos { get; set; } = new();
    public List<CeldaItemComboDto> CeldasDisponibles { get; set; } = new();
}

public class ProyectoNodoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public List<LineaNodoDto> Lineas { get; set; } = new();
}

public class LineaNodoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public List<CeldaNodoDto> Celdas { get; set; } = new();
}

public class CeldaNodoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public List<string> Estaciones { get; set; } = new();
}

public class CeldaItemComboDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
}

public class ParteAsignadaDto
{
    public int Id { get; set; }
    public string SapPartNumber { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}