using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Controllers;

public class TurnosController : Controller
{
    private readonly AppDbContext _context;

    public TurnosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int plantaId = ObtenerPlantaActivaId();

        var turnosDb = await _context.Turnos
            .Where(t => t.PlantaId == plantaId)
            .OrderBy(t => t.HoraInicio)
            .ToListAsync();

        var turnos = turnosDb.Select(t =>
        {
            double duracion = t.HoraFin <= t.HoraInicio
                ? (t.HoraFin.Add(TimeSpan.FromDays(1)) - t.HoraInicio).TotalHours
                : (t.HoraFin - t.HoraInicio).TotalHours;

            string clave = "T1";
            string claseCss = "t1";
            string colorHex = "#2563eb";
            var upper = (t.Nombre ?? "").ToUpperInvariant();

            if (upper.Contains("2") || upper.Contains("VESP"))
            {
                clave = "T2";
                claseCss = "t2";
                colorHex = "#d97706";
            }
            else if (upper.Contains("3") || upper.Contains("NOCT") || upper.Contains("12H-N"))
            {
                clave = "T3";
                claseCss = "t3";
                colorHex = "#1e293b";
            }

            return new TurnoViewModel
            {
                Id = t.Id,
                PlantaId = t.PlantaId,
                Clave = clave,
                Nombre = t.Nombre,
                HoraInicio = t.HoraInicio,
                HoraFin = t.HoraFin,
                Horas = Math.Round(duracion, 1),
                ClaseColor = claseCss,
                ColorHex = colorHex,
                Activo = t.Activo
            };
        }).ToList();

        return View(turnos);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTurno(int id)
    {
        var t = await _context.Turnos.FindAsync(id);
        if (t == null) return NotFound();

        return Json(new
        {
            id = t.Id,
            nombre = t.Nombre,
            horaInicio = t.HoraInicio.ToString(@"hh\:mm"),
            horaFin = t.HoraFin.ToString(@"hh\:mm"),
            activo = t.Activo
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int id, string nombre, TimeSpan horaInicio, TimeSpan horaFin, bool activo)
    {
        int plantaId = ObtenerPlantaActivaId();

        if (id == 0)
        {
            var nuevoTurno = new Turno
            {
                PlantaId = plantaId,
                Nombre = nombre,
                HoraInicio = horaInicio,
                HoraFin = horaFin,
                Activo = activo
            };
            _context.Turnos.Add(nuevoTurno);
        }
        else
        {
            var turnoDb = await _context.Turnos.FindAsync(id);
            if (turnoDb == null) return NotFound();

            turnoDb.Nombre = nombre;
            turnoDb.HoraInicio = horaInicio;
            turnoDb.HoraFin = horaFin;
            turnoDb.Activo = activo;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
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