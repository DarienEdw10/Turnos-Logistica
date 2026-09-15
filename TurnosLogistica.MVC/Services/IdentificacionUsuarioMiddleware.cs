using TurnosLogistica.MVC.Services;

namespace TurnosLogistica.MVC.Middlewares;

public class IdentificacionUsuarioMiddleware
{
    private readonly RequestDelegate _next;

    public IdentificacionUsuarioMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UsuarioAuthService authService)
    {
        // 1. Detectar CWID desde Windows SSO o fallback de entorno (Sin simulador)
        string cwidRaw = context.User.Identity?.Name ?? string.Empty;

        // Fallback en desarrollo: usuario de la máquina
        if (string.IsNullOrWhiteSpace(cwidRaw))
        {
            cwidRaw = Environment.UserName;
        }

        string cwidLimpio = UsuarioAuthService.LimpiarCwid(cwidRaw);

        // 2. Verificar si ya está identificado en sesión con ese mismo CWID
        var claimCwidActual = context.User.FindFirst("CWID")?.Value;
        bool tienePlantaAsignada = context.User.HasClaim(c => c.Type == "PlantaAsignadaId");

        if (claimCwidActual != cwidLimpio || !tienePlantaAsignada)
        {
            // Ejecuta la identificación silenciosa con tu servicio usando la identidad real
            await authService.IdentificarUsuarioAutomaticoAsync(cwidLimpio, context);
        }

        await _next(context);
    }
}