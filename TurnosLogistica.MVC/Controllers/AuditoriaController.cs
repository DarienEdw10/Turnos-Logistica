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
        int plantaId = ObtenerPlantaActivaId();
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

        vm.Registros = await _auditoriaRepo.ConsultarHistorialAsync(plantaId, fInicio, fFin);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCsv(DateTime? fechaInicio, DateTime? fechaFin)
    {
        int plantaId = ObtenerPlantaActivaId();
        DateTime fInicio = fechaInicio ?? DateTime.Today.AddDays(-30);
        DateTime fFin = fechaFin ?? DateTime.Today;

        if ((fFin - fInicio).TotalDays > 365 || fFin < fInicio)
        {
            return BadRequest("Rango de fechas inválido o mayor a 365 días.");
        }

        var registros = await _auditoriaRepo.ConsultarHistorialAsync(plantaId, fInicio, fFin);

        var sb = new StringBuilder();
        sb.AppendLine("ID,FECHA_HORA,USUARIO,ROL,ACCION,AGENDA_CAMBIO,RAZON");

        foreach (var r in registros)
        {
            string cleanRazon = r.Razon.Replace("\"", "\"\"");
            string cleanAgenda = r.AgendaDetalle.Replace("\"", "\"\"");
            sb.AppendLine($"{r.HistorialId},{r.FechaAccion:dd/MM/yyyy HH:mm},{r.UsuarioResponsable},{r.RolUsuario},{r.Accion},\"{cleanAgenda}\",\"{cleanRazon}\"");
        }

        byte[] buffer = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(buffer, "text/csv", $"Auditoria_Planta{plantaId}_{DateTime.Now:yyyyMMdd_HHmm}.csv");
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