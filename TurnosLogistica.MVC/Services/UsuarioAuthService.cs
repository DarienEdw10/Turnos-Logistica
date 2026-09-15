using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
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
        >= 15 => "programador_logistica", // <-- NUEVO: Mapeo para el nivel 15
        _ => "operador"
    };

    public async Task<Usuario> SincronizarUsuarioAsync(
        string cwid,
        string noEmpleado,
        string nombre,
        string email,
        int nivel,
        int plantaId)
    {
        string cwidLimpio = LimpiarCwid(cwid);

        // Corrección: Validar que u.CWID no sea null antes de invocar .ToUpper()
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => (u.CWID != null && u.CWID.ToUpper() == cwidLimpio) || u.NoEmpleado == noEmpleado);

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
        
        // Corrección: Validar u.CWID != null
        var u = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CWID != null && x.CWID.ToUpper() == cwidLimpio && x.Activo);

        return u?.Nivel ?? 0;
    }

    public async Task<(bool Exito, string Mensaje, Usuario? Usuario)> IdentificarUsuarioAutomaticoAsync(
        string rawCwid,
        HttpContext httpContext)
    {
        string cwid = LimpiarCwid(rawCwid);
        if (string.IsNullOrWhiteSpace(cwid))
        {
            return (false, "No se detectó CWID o identificador en la sesión.", null);
        }

        // Corrección: Validar u.CWID != null
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => (u.CWID != null && u.CWID.ToUpper() == cwid) || u.NoEmpleado == cwid);

        if (usuario == null)
        {
            usuario = new Usuario
            {
                //GENERA UN USUARIO TEMPORAL CON ROL DE OPERADOR Y NIVEL 10
                //EN CASO DE QUE NO EXISTA EN LA BASE DE DATOS
                PlantaId = 1,
                CWID = cwid,
                NoEmpleado = cwid,
                Nombre = cwid,
                Email = $"{cwid.ToLower()}@autotek.com",
                Rol = "operador",
                Nivel = 10,
                Activo = true,
                CreadoAt = DateTime.UtcNow
            };
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
        }

        if (!usuario.Activo)
        {
            return (false, "El usuario identificado se encuentra inactivo.", null);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim("CWID", usuario.CWID ?? usuario.NoEmpleado),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim(ClaimTypes.Role, usuario.Rol.ToLowerInvariant()),
            new Claim("NivelJerarquico", usuario.Nivel.ToString()),
            new Claim("PlantaAsignadaId", usuario.PlantaId.ToString()),
            new Claim("PlantaAsignadaNombre", $"Planta {usuario.PlantaId}")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        httpContext.User = principal;
        if (!httpContext.Request.Cookies.ContainsKey("PlantaActivaId"))
        {
            httpContext.Response.Cookies.Append("PlantaActivaId", usuario.PlantaId.ToString(), new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                SameSite = SameSiteMode.Lax
            });
        }

        return (true, "Identificado automáticamente.", usuario);
    }

    public async Task<(bool Exito, string Mensaje, Usuario? Usuario)> AutenticarEIniciarSesionAsync(
        string rawCwid,
        string password,
        HttpContext httpContext)
    {
        string cwid = LimpiarCwid(rawCwid);
        if (string.IsNullOrWhiteSpace(cwid))
        {
            return (false, "Debe ingresar su CWID / Nómina.", null);
        }

        bool validado = false;
        string nombreCorp = string.Empty;
        string emailCorp = string.Empty;
        string noEmpleadoCorp = cwid;
        int plantaIdCorp = 0;
        int nivelCorp = 10;

        if (!validado)
        {
            // Corrección: Validar u.CWID != null
            var usuarioPrueba = await _context.Usuarios
                .FirstOrDefaultAsync(u => ((u.CWID != null && u.CWID.ToUpper() == cwid) || u.NoEmpleado == cwid) && u.Activo);

            if (usuarioPrueba != null)
            {
                validado = true;
                nombreCorp = usuarioPrueba.Nombre;
                emailCorp = usuarioPrueba.Email;
                noEmpleadoCorp = usuarioPrueba.NoEmpleado;
                nivelCorp = usuarioPrueba.Nivel;
                plantaIdCorp = usuarioPrueba.PlantaId;
            }
        }

        if (!validado)
        {
            return (false, "El usuario o CWID ingresado no existe o se encuentra inactivo.", null);
        }

        // Corrección: Validar u.CWID != null
        var usuarioLocal = await _context.Usuarios
            .FirstOrDefaultAsync(u => (u.CWID != null && u.CWID.ToUpper() == cwid) || u.NoEmpleado == noEmpleadoCorp);

        Usuario usuarioFinal;
        if (usuarioLocal != null)
        {
            if (!usuarioLocal.Activo)
            {
                return (false, "Su usuario se encuentra inactivo en la plataforma.", null);
            }

            usuarioFinal = await SincronizarUsuarioAsync(
                cwid,
                usuarioLocal.NoEmpleado,
                string.IsNullOrEmpty(nombreCorp) ? usuarioLocal.Nombre : nombreCorp,
                string.IsNullOrEmpty(emailCorp) ? usuarioLocal.Email : emailCorp,
                usuarioLocal.Nivel,
                usuarioLocal.PlantaId
            );
        }
        else
        {
            usuarioFinal = await SincronizarUsuarioAsync(
                cwid,
                noEmpleadoCorp,
                nombreCorp,
                emailCorp,
                nivelCorp,
                plantaIdCorp > 0 ? plantaIdCorp : 1
            );
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuarioFinal.Id.ToString()),
            new Claim("CWID", usuarioFinal.CWID ?? usuarioFinal.NoEmpleado),
            new Claim(ClaimTypes.Name, usuarioFinal.Nombre),
            new Claim(ClaimTypes.Role, usuarioFinal.Rol.ToLowerInvariant()),
            new Claim("NivelJerarquico", usuarioFinal.Nivel.ToString()),
            new Claim("PlantaAsignadaId", usuarioFinal.PlantaId.ToString()),
            new Claim("PlantaAsignadaNombre", $"Planta {usuarioFinal.PlantaId}")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        httpContext.Response.Cookies.Append("PlantaActivaId", usuarioFinal.PlantaId.ToString(), new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            SameSite = SameSiteMode.Lax
        });

        return (true, "Acceso concedido.", usuarioFinal);
    }

}