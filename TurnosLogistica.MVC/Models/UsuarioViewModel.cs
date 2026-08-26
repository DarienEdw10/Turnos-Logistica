namespace TurnosLogistica.MVC.Models;

public class UsuarioViewModel
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public string NoEmpleado { get; set; } = "";
    public string Email { get; set; } = "";
    public string PlantaNombre { get; set; } = "";
    public string Rol { get; set; } = "operador"; // sistemas, admin, jefe_log, operador
    public bool Activo { get; set; } = true;
    public DateTime? UltimoAcceso { get; set; }
}