using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Models;
using TurnosLogistica.MVC.Services;

namespace TurnosLogistica.MVC.Controllers;

public class ProgramacionController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPlanificacionService _service;

    public ProgramacionController(AppDbContext context, IPlanificacionService service)
    {
        _context = context;
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int plantaId = ObtenerPlantaActivaId();

        ViewBag.Proyectos = await _context.Proyectos
            .Where(p => p.PlantaId == plantaId && p.Activo)
            .OrderBy(p => p.Codigo)
            .ToListAsync();

        var turnosDb = await _context.Turnos
            .Where(t => t.PlantaId == plantaId && t.Activo)
            .OrderBy(t => t.HoraInicio)
            .ToListAsync();

        ViewBag.Turnos = turnosDb.Select(t =>
        {
            double duracion = t.HoraFin <= t.HoraInicio
                ? (t.HoraFin.Add(TimeSpan.FromDays(1)) - t.HoraInicio).TotalHours
                : (t.HoraFin - t.HoraInicio).TotalHours;

            string color = "t1";
            var nombreUpper = (t.Nombre ?? "").ToUpperInvariant();
            if (nombreUpper.StartsWith("2") || nombreUpper.Contains("VESPERTINO")) color = "t2";
            else if (nombreUpper.StartsWith("3") || nombreUpper.Contains("NOCTURNO")) color = "t3";

            return new
            {
                Id = t.Id,
                Nombre = t.Nombre,
                Horario = $"{t.HoraInicio:hh\\:mm} – {t.HoraFin:hh\\:mm}",
                Horas = Math.Round(duracion, 1),
                ClaseColor = color
            };
        }).ToList();

        return View(new ProgramacionFormViewModel());
    }
    [HttpGet]
    public async Task<IActionResult> ObtenerLineasPorProyecto(int proyectoId)
    {
        var lineas = await _context.Lineas
            .Where(l => l.ProyectoId == proyectoId && l.Activa)
            .Select(l => new { id = l.Id, texto = l.Nombre ?? l.Codigo })
            .ToListAsync();
        return Json(lineas);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerCeldasPorLinea(int lineaId)
    {
        var celdas = await _context.Celdas
            .Where(c => c.LineaId == lineaId && c.Activa)
            .Select(c => new { id = c.Id, texto = c.Codigo + " — " + c.Nombre })
            .ToListAsync();
        return Json(celdas);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerPartesPorCelda(int celdaId)
    {
        var partes = await _context.NumerosDeParte
            .Where(p => p.CeldaId == celdaId && p.Activo)
            .Select(p => new { id = p.Id, texto = p.SapPartNumber + " — " + p.Descripcion })
            .ToListAsync();
        return Json(partes);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTurnosPorPlanta(int? plantaId = null)
    {
        int pId = plantaId ?? ObtenerPlantaActivaId();

        var turnosDb = await _context.Turnos
            .Where(t => t.PlantaId == pId && t.Activo)
            .OrderBy(t => t.HoraInicio)
            .ToListAsync();

        var result = turnosDb.Select(t => new
        {
            id = t.Id,
            nombre = t.Nombre,
            horario = $"{t.HoraInicio:hh\\:mm} – {t.HoraFin:hh\\:mm}",
            horas = Math.Round(t.DuracionHoras, 1),
            claseColor = t.ClaseColor
        });

        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(ProgramacionFormViewModel model)
    {
        int plantaId = ObtenerPlantaActivaId();

        if (!ModelState.IsValid)
        {
            ViewBag.Proyectos = await _context.Proyectos
                .Where(p => p.PlantaId == plantaId && p.Activo)
                .OrderBy(p => p.Codigo)
                .ToListAsync();

            var turnosDb = await _context.Turnos
                .Where(t => t.PlantaId == plantaId && t.Activo)
                .OrderBy(t => t.HoraInicio)
                .ToListAsync();

            ViewBag.Turnos = turnosDb.Select(t => new
            {
                Id = t.Id,
                Nombre = t.Nombre,
                Horario = $"{t.HoraInicio:hh\\:mm} – {t.HoraFin:hh\\:mm}",
                Horas = Math.Round(t.DuracionHoras, 1),
                ClaseColor = t.ClaseColor
            }).ToList();

            return View("Index", model);
        }

        await _service.GuardarProgramacionAsync(model, usuarioId: 1);
        return RedirectToAction("Index", "Calendario", new { mes = model.FechaProduccion.Month, anio = model.FechaProduccion.Year });
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