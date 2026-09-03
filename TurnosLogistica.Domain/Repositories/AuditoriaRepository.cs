using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;

namespace TurnosLogistica.Domain.Repositories;

public class AuditoriaRepository : IAuditoriaRepository
{
    private readonly AppDbContext _context;

    public AuditoriaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<RegistroAuditoriaDto>> ConsultarHistorialAsync(int plantaId, DateTime fechaInicio, DateTime fechaFin)
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
            string accionClase = "t1";
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
}