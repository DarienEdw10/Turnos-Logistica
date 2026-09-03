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

        // 1. Cargar datos reales de la planta activa
        var proyectosDb = await _context.Proyectos
            .Where(p => p.PlantaId == plantaId && p.Activo)
            .OrderBy(p => p.Codigo)
            .ToListAsync();

        var lineasDb = await _context.Lineas
            .Where(l => l.PlantaId == plantaId && l.Activa)
            .OrderBy(l => l.Nombre)
            .ToListAsync();

        var celdasDb = await (from c in _context.Celdas
                              join l in _context.Lineas on c.LineaId equals l.Id
                              where l.PlantaId == plantaId && c.Activa
                              orderby c.Codigo
                              select c).ToListAsync();

        var celdaIds = celdasDb.Select(c => c.Id).ToList();

        // Cargar únicamente las estaciones reales registradas en SQL Server
        var estacionesDb = await _context.Estaciones
            .AsNoTracking()
            .Where(e => celdaIds.Contains(e.CeldaId) && e.Activa)
            .OrderBy(e => e.Codigo)
            .ToListAsync();

        // 2. Jerarquía fiel a la base de datos
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
                    // CORREGIDO: Cargar solo estaciones reales de BD
                    Estaciones = estacionesDb
                        .Where(e => e.CeldaId == c.Id)
                        .Select(e => $"{e.Codigo} — {e.Nombre}")
                        .ToList()
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

        ViewBag.ProyectosJson = proyectosDb.Select(p => new { id = p.Id, texto = p.Codigo + " — " + p.Nombre }).ToList();
        ViewBag.LineasJson = lineasDb.Select(l => new { id = l.Id, texto = (l.Nombre ?? l.Codigo) + " (Línea)" }).ToList();
        ViewBag.CeldasJson = celdasDb.Select(c => new { id = c.Id, texto = c.Codigo + " — " + c.Nombre }).ToList();
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
        else if (tipoNodo == "estacion")
        {
            _context.Estaciones.Add(new Estacion
            {
                CeldaId = padreId,
                Nombre = nombre,
                Codigo = codigo,
                Activa = true
            });
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerWorkCentersPorLinea(int lineaId)
    {
        if (lineaId <= 0)
            return Json(new { workCenters = new List<string>(), sugerido = "" });

        var codigosExistentes = await _context.Celdas
            .AsNoTracking()
            .Where(c => c.LineaId == lineaId && c.Activa)
            .OrderBy(c => c.Codigo)
            .Select(c => c.Codigo)
            .Distinct()
            .ToListAsync();

        var partesCeldaIds = await _context.NumerosDeParte
            .AsNoTracking()
            .Where(np => np.LineaId == lineaId && np.CeldaId != null)
            .Select(np => np.CeldaId!.Value)
            .Distinct()
            .ToListAsync();

        var codigosDesdePartes = await _context.Celdas
            .AsNoTracking()
            .Where(c => partesCeldaIds.Contains(c.Id))
            .Select(c => c.Codigo)
            .Distinct()
            .ToListAsync();

        var todosLosWorkCenters = codigosExistentes
            .Union(codigosDesdePartes)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .OrderBy(c => c)
            .ToList();

        string codigoSugerido = todosLosWorkCenters.FirstOrDefault() ?? "";

        return Json(new
        {
            workCenters = todosLosWorkCenters,
            sugerido = codigoSugerido
        });
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerProyectosPorPlanta()
    {
        int plantaId = ObtenerPlantaActivaId();

        var proyectos = await _context.Proyectos
            .AsNoTracking()
            .Where(p => p.PlantaId == plantaId && p.Activo)
            .OrderBy(p => p.Codigo)
            .Select(p => new
            {
                id = p.Id,
                codigo = p.Codigo,
                nombre = p.Nombre,
                texto = $"{p.Codigo} — {p.Nombre}"
            })
            .ToListAsync();

        return Json(proyectos);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerLineasPorProyecto(int proyectoId)
    {
        if (proyectoId <= 0)
            return Json(new List<object>());

        var lineas = await _context.Lineas
            .AsNoTracking()
            .Where(l => l.ProyectoId == proyectoId && l.Activa)
            .OrderBy(l => l.Nombre)
            .Select(l => new
            {
                id = l.Id,
                codigo = l.Codigo ?? "",
                nombre = l.Nombre,
                texto = $"{l.Nombre} ({(string.IsNullOrEmpty(l.Codigo) ? "S/C" : l.Codigo)})"
            })
            .ToListAsync();

        return Json(lineas);
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