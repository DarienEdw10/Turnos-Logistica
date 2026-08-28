using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;

namespace TurnosLogistica.MVC.Services;

public class UsuarioAuthService
{
    private readonly AppDbContext _context;

    public UsuarioAuthService(AppDbContext context)
    {
        _context = context;
    }

    public static string LimpiarCwid(string rawCwid)
    {
        if (string.IsNullOrWhiteSpace(rawCwid)) return string.Empty;
        if (rawCwid.Contains('\\'))
        {
            rawCwid = rawCwid.Split('\\')[1];
        }
        return rawCwid.Trim().ToUpperInvariant();
    }

    public static string MapearRolSegunNivel(int nivel) => nivel switch
    {
        >= 40 => "sistemas",
        >= 30 => "admin",
        >= 20 => "jefe_log",
        _ => "operador"
    };

    public async Task<Usuario> SincronizarUsuarioAsync(string cwid, string noEmpleado, string nombre, string email, int nivel, int plantaId)
    {
        string cwidLimpio = LimpiarCwid(cwid);

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.CWID == cwidLimpio || u.NoEmpleado == noEmpleado);

        string rol = MapearRolSegunNivel(nivel);

        if (usuario != null)
        {
            usuario.CWID = cwidLimpio;
            usuario.Nivel = nivel;
            usuario.Rol = rol;
            usuario.Activo = true;
            if (!string.IsNullOrEmpty(nombre)) usuario.Nombre = nombre;
            if (!string.IsNullOrEmpty(email)) usuario.Email = email;
            if (plantaId > 0) usuario.PlantaId = plantaId;
        }
        else
        {
            usuario = new Usuario
            {
                PlantaId = plantaId > 0 ? plantaId : 1,
                CWID = cwidLimpio,
                NoEmpleado = string.IsNullOrEmpty(noEmpleado) ? cwidLimpio : noEmpleado,
                Nombre = string.IsNullOrEmpty(nombre) ? cwidLimpio : nombre,
                Email = string.IsNullOrEmpty(email) ? $"{cwidLimpio.ToLower()}@autotek.com" : email,
                Rol = rol,
                Nivel = nivel,
                Activo = true,
                CreadoAt = DateTime.UtcNow
            };
            _context.Usuarios.Add(usuario);
        }

        await _context.SaveChangesAsync();
        return usuario;
    }

    public async Task<int> ObtenerNivelPorCwidAsync(string cwid)
    {
        string cwidLimpio = LimpiarCwid(cwid);
        var u = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CWID == cwidLimpio && x.Activo);

        return u?.Nivel ?? 0;
    }
}