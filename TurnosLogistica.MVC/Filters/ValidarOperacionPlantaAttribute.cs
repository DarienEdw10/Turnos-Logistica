using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;

namespace TurnosLogistica.MVC.Filters;

public class ValidarOperacionPlantaAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        // 1. Leer claims del usuario actual
        string? plantaAsignadaClaim = user.FindFirst("PlantaAsignadaId")?.Value;
        int.TryParse(plantaAsignadaClaim, out int plantaAsignadaId);

        string rol = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value?.ToLower() ?? "operador";
        int.TryParse(user.FindFirst("NivelJerarquico")?.Value, out int nivel);

        // 2. Leer la planta que se está intentando modificar
        int plantaOperadaId = plantaAsignadaId;
        if (httpContext.Request.Cookies.TryGetValue("PlantaActivaId", out string? cVal) && int.TryParse(cVal, out int pCookie))
        {
            plantaOperadaId = pCookie;
        }

        // REGLA 1: Si opera su propia planta -> Permite el guardado normalmente
        if (plantaAsignadaId == plantaOperadaId)
        {
            await next();
            return;
        }

        // REGLA 2: Si opera OTRA planta y NO es Sistemas (nivel < 40) -> Bloqueo total
        if (nivel < 40 && rol != "sistemas")
        {
            context.Result = new JsonResult(new
            {
                success = false,
                message = $"Modo Solo Lectura: Tu planta asignada es la Planta {plantaAsignadaId}. En la Planta {plantaOperadaId} no tienes permisos para realizar cambios."
            })
            { StatusCode = 403 };
            return;
        }

        // REGLA 3: Es SISTEMAS realizando apoyo en otra planta -> Ejecutar y auditar
        var dbContext = httpContext.RequestServices.GetRequiredService<AppDbContext>();

        // Justificación enviada en headers, query o form
        string razon = httpContext.Request.Headers["X-Razon-Apoyo-Sistemas"].ToString();
        if (string.IsNullOrWhiteSpace(razon))
        {
            razon = httpContext.Request.Query["razonCambio"].ToString();
        }
        if (string.IsNullOrWhiteSpace(razon))
        {
            razon = "Soporte técnico / Cobertura de Sistemas por falta de personal en planta destino.";
        }

        // Proceder con la operación en base de datos
        await next();

        // Registrar en la tabla Historial_agenda
        try
        {
            int usuarioId = int.Parse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "1");
            string cwid = user.FindFirst("CWID")?.Value ?? "SISTEMAS";

            var bitacora = new HistorialAgenda
            {
                ProgramacionId = 0,
                Accion = "APOYO_SISTEMAS_INTERPLANTA",
                Razon = $"[SISTEMAS: {cwid} de Planta {plantaAsignadaId} modificó en Planta {plantaOperadaId}] Motivo: {razon}",
                UsuarioId = usuarioId,
                FechaAccion = DateTime.UtcNow,
                ValorNuevo = $"Controlador: {context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}"
            };

            dbContext.HistorialAgendas.Add(bitacora);
            await dbContext.SaveChangesAsync();
        }
        catch
        {
            // Evitar interrumpir la respuesta si falla la inserción secundaria
        }
    }
}