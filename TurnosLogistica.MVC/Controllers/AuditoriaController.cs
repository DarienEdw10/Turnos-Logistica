using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Controllers;

public class AuditoriaController : Controller
{
    private readonly AppDbContext _context;

    public AuditoriaController(AppDbContext context)
    {
        _context = context;
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

        vm.Registros = await ConsultarHistorialSpAsync(plantaId, fInicio, fFin);
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

        var registros = await ConsultarHistorialSpAsync(plantaId, fInicio, fFin);

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

    private async Task<List<RegistroAuditoriaDto>> ConsultarHistorialSpAsync(int plantaId, DateTime fechaInicio, DateTime fechaFin)
    {
        var lista = new List<RegistroAuditoriaDto>();

        var pPlanta = new SqlParameter("@PlantaId", plantaId);
        var pInicio = new SqlParameter("@FechaInicio", fechaInicio.ToString("yyyy-MM-dd"));
        var pFin = new SqlParameter("@FechaFin", fechaFin.ToString("yyyy-MM-dd"));

        string sql = "EXEC mps.SP_ConsultarHistorialAuditoria @PlantaId, @FechaInicio, @FechaFin";

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(pPlanta);
        command.Parameters.Add(pInicio);
        command.Parameters.Add(pFin);

        await _context.Database.OpenConnectionAsync();
        using var result = await command.ExecuteReaderAsync();

        while (await result.ReadAsync())
        {
            string accion = result["accion"]?.ToString() ?? "ALTA";
            string accionClase = "t1"; // default
            string accionBadge = accion.ToUpper();

            if (accionBadge.Contains("CREAC") || accionBadge.Contains("ALTA"))
            {
                accionClase = "activo";
            }
            else if (accionBadge.Contains("REPROG") || accionBadge.Contains("MODIF"))
            {
                accionClase = "t2";
            }
            else if (accionBadge.Contains("CANCEL"))
            {
                accionClase = "inactivo";
            }

            string sap = result["sap_part_number"]?.ToString() ?? "";
            string turno = result["Turno"]?.ToString() ?? "";
            DateTime fProg = Convert.ToDateTime(result["FechaProgramada"]);

            lista.Add(new RegistroAuditoriaDto
            {
                HistorialId = Convert.ToInt64(result["HistorialId"]),
                FechaAccion = Convert.ToDateTime(result["fecha_accion"]),
                UsuarioResponsable = result["UsuarioResponsable"]?.ToString() ?? "",
                RolUsuario = result["RolUsuario"]?.ToString() ?? "",
                Accion = accion,
                AccionBadgeClase = accionClase,
                SapPartNumber = sap,
                NoDeParte = result["no_de_parte"]?.ToString() ?? "",
                FechaProgramada = fProg,
                Turno = turno,
                AgendaDetalle = $"{sap} / {turno} / {fProg:dd-MMM}",
                Razon = result["razon"]?.ToString() ?? ""
            });
        }

        return lista;
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