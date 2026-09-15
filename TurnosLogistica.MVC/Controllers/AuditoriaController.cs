using System.Text;
using Microsoft.AspNetCore.Mvc;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.Domain.Repositories;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Controllers;

public class AuditoriaController : Controller
{
    private readonly IAuditoriaRepository _auditoriaRepo;

    public AuditoriaController(IAuditoriaRepository auditoriaRepo)
    {
        _auditoriaRepo = auditoriaRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fechaInicio, DateTime? fechaFin)
    {
        // 1. Candado de seguridad: solo Admin (30) y Sistemas (40)
        if (!TienePermisoAuditoria())
        {
            return Forbid();
        }

        int plantaId = ObtenerPlantaActivaId();
        ViewBag.PlantaActivaId = plantaId;

        DateTime fInicio = fechaInicio ?? DateTime.Today.AddDays(-30);
        DateTime fFin = fechaFin ?? DateTime.Today;

        var vm = new AuditoriaViewModel
        {
            FechaInicio = fInicio,
            FechaFin = fFin
        };

        if ((fFin - fInicio).TotalDays > 365)
        {
            vm.ErrorMensaje = "Consulta bloqueada: No se permiten consultas con un rango mayor a 365 días.";
            return View(vm);
        }

        if (fFin < fInicio)
        {
            vm.ErrorMensaje = "La fecha final no puede ser menor a la inicial.";
            return View(vm);
        }

        var registros = await _auditoriaRepo.ConsultarHistorialAsync(plantaId, fInicio, fFin);

        // Ajuste de zona horaria de UTC a Hora de México
        AjustarHorarioRegistros(registros);

        vm.Registros = registros;
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCsv(DateTime? fechaInicio, DateTime? fechaFin)
    {
        // 1. Candado de seguridad: solo Admin (30) y Sistemas (40)
        if (!TienePermisoAuditoria())
        {
            return Forbid();
        }

        int plantaId = ObtenerPlantaActivaId();
        DateTime fInicio = fechaInicio ?? DateTime.Today.AddDays(-30);
        DateTime fFin = fechaFin ?? DateTime.Today;

        if ((fFin - fInicio).TotalDays > 365 || fFin < fInicio)
        {
            return BadRequest("Rango de fechas inválido o mayor a 365 días.");
        }

        var registros = await _auditoriaRepo.ConsultarHistorialAsync(plantaId, fInicio, fFin);

        // Ajuste de zona horaria de UTC a Hora de México
        AjustarHorarioRegistros(registros);

        var sb = new StringBuilder();
        sb.AppendLine("ID,FECHA_HORA,USUARIO,ROL,ACCION,AGENDA_CAMBIO,RAZON");

        foreach (var r in registros)
        {
            string cleanRazon = (r.Razon ?? string.Empty).Replace("\"", "\"\"");
            string cleanAgenda = (r.AgendaDetalle ?? string.Empty).Replace("\"", "\"\"");
            sb.AppendLine($"{r.HistorialId},{r.FechaAccion:dd/MM/yyyy HH:mm},{r.UsuarioResponsable},{r.RolUsuario},{r.Accion},\"{cleanAgenda}\",\"{cleanRazon}\"");
        }

        byte[] buffer = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(buffer, "text/csv; charset=utf-8", $"Auditoria_Planta{plantaId}_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    private void AjustarHorarioRegistros(IEnumerable<dynamic> registros)
    {
   TimeZoneInfo tzMexico;
        try
        {
            tzMexico = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }
        catch
        {
            tzMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        }

        foreach (var r in registros)
        {
            DateTime fechaUtc = r.FechaAccion.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(r.FechaAccion, DateTimeKind.Utc) 
                : r.FechaAccion.ToUniversalTime();

            // Convertimos y le restamos exactamente 1 hora para eliminar el desfase visual
            r.FechaAccion = TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, tzMexico).AddHours(-1);
        }
    }

    private bool TienePermisoAuditoria()
    {
        string? nivelStr = User.FindFirst("NivelJerarquico")?.Value;
        string rol = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value?.ToLower() ?? string.Empty;

        if (int.TryParse(nivelStr, out int nivel) && nivel >= 30)
        {
            return true;
        }

        return rol == "admin" || rol == "sistemas";
    }

    private int ObtenerPlantaActivaId()
    {
        if (Request.Cookies.TryGetValue("PlantaActivaId", out string? idStr) && int.TryParse(idStr, out int idVal))
        {
            return idVal;
        }
        return 1;
    }
}