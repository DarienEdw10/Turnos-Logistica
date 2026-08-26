namespace TurnosLogistica.MVC.Models;

public class EstructuraViewModel
{
    public List<PlantaNodoDto> Plantas { get; set; } = new();
    public int CeldaSeleccionadaId { get; set; }
    public List<string> PartesAsignadasACelda { get; set; } = new();
}

public class PlantaNodoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public List<ProyectoNodoDto> Proyectos { get; set; } = new();
}

public class ProyectoNodoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public List<LineaNodoDto> Lineas { get; set; } = new();
}

public class LineaNodoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public List<CeldaNodoDto> Celdas { get; set; } = new();
}

public class CeldaNodoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public List<EstacionNodoDto> Estaciones { get; set; } = new();
}

public class EstacionNodoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}