using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Controllers;

public class EstructuraController : Controller
{
    private readonly AppDbContext _context;

    public EstructuraController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int plantaId = ObtenerPlantaActivaId();

        var planta = await _context.Plantas.FindAsync(plantaId);
        string plantaNombre = planta != null ? $"{planta.Nombre} — {planta.Codigo}" : "Planta";

        // 1. Cargar datos de la planta activa
        var proyectosDb = await _context.Proyectos
            .Where(p => p.PlantaId == plantaId && p.Activo)
            .ToListAsync();

        var lineasDb = await _context.Lineas
            .Where(l => l.PlantaId == plantaId && l.Activa)
            .ToListAsync();

        var celdasDb = await (from c in _context.Celdas
                              join l in _context.Lineas on c.LineaId equals l.Id
                              where l.PlantaId == plantaId && c.Activa
                              select c).ToListAsync();

        // 2. Jerarquía
        var proyectosDto = proyectosDb.Select(p => new ProyectoNodoDto
        {
            Id = p.Id,
            Codigo = p.Codigo,
            Nombre = p.Nombre,
            Lineas = lineasDb.Where(l => l.ProyectoId == p.Id).Select(l => new LineaNodoDto
            {
                Id = l.Id,
                Codigo = l.Codigo,
                Nombre = l.Nombre ?? l.Codigo,
                Celdas = celdasDb.Where(c => c.LineaId == l.Id).Select(c => new CeldaNodoDto
                {
                    Id = c.Id,
                    Codigo = c.Codigo,
                    Nombre = c.Nombre,
                    Estaciones = new List<string> { "EST-01 Carga", "EST-02 Ensamble / Soldadura", "EST-03 Descarga / Calidad" }
                }).ToList()
            }).ToList()
        }).ToList();

        var celdasCombo = (from c in celdasDb
                           join l in lineasDb on c.LineaId equals l.Id
                           select new CeldaItemComboDto
                           {
                               Id = c.Id,
                               Texto = $"{c.Codigo} — {c.Nombre} ({l.Nombre ?? l.Codigo})"
                           }).ToList();

        // Pasar proyectos y líneas para el modal
        ViewBag.ProyectosJson = proyectosDb.Select(p => new { id = p.Id, texto = p.Codigo + " — " + p.Nombre }).ToList();
        ViewBag.LineasJson = lineasDb.Select(l => new { id = l.Id, texto = (l.Nombre ?? l.Codigo) + " (Línea)" }).ToList();

        var vm = new EstructuraViewModel
        {
            PlantaId = plantaId,
            PlantaNombre = plantaNombre,
            Proyectos = proyectosDto,
            CeldasDisponibles = celdasCombo
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerPartesPorCelda(int celdaId)
    {
        var partes = await _context.NumerosDeParte
            .Where(p => p.CeldaId == celdaId && p.Activo)
            .Select(p => new ParteAsignadaDto
            {
                Id = p.Id,
                SapPartNumber = p.SapPartNumber,
                Descripcion = p.Descripcion
            })
            .ToListAsync();

        return Json(partes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesvincularParte(int parteId)
    {
        var parte = await _context.NumerosDeParte.FindAsync(parteId);
        if (parte != null)
        {
            parte.CeldaId = null;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        return Json(new { success = false, message = "Parte no encontrada" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarNodo(string tipoNodo, int padreId, string nombre, string codigo)
    {
        int plantaId = ObtenerPlantaActivaId();

        if (tipoNodo == "proyecto")
        {
            _context.Proyectos.Add(new Proyecto { PlantaId = plantaId, Nombre = nombre, Codigo = codigo, Activo = true });
        }
        else if (tipoNodo == "linea")
        {
            _context.Lineas.Add(new Linea { PlantaId = plantaId, ProyectoId = padreId, Nombre = nombre, Codigo = codigo, Activa = true });
        }
        else if (tipoNodo == "celda")
        {
            _context.Celdas.Add(new Celda { LineaId = padreId, Nombre = nombre, Codigo = codigo, Activa = true });
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