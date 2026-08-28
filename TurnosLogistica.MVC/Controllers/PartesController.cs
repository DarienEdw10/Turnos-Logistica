using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models;
using TurnosLogistica.MVC.Models;

namespace TurnosLogistica.MVC.Controllers;

public class PartesController : Controller
{
    private readonly AppDbContext _context;

    public PartesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int plantaId = ObtenerPlantaActivaId();

        var query = from parte in _context.NumerosDeParte
                    join linea in _context.Lineas on parte.LineaId equals linea.Id into lJ
                    from linea in lJ.DefaultIfEmpty()
                    join celda in _context.Celdas on parte.CeldaId equals (int?)celda.Id into cJ
                    from celda in cJ.DefaultIfEmpty()
                    where parte.PlantaId == plantaId && parte.Activo
                    orderby parte.SapPartNumber
                    select new ParteItemDto
                    {
                        Id = parte.Id,
                        SapPartNumber = parte.SapPartNumber,
                        NoDeParte = parte.NoDeParte,
                        Descripcion = parte.Descripcion,
                        FinalAssembly = parte.FinalAssembly ?? "—",
                        Familia = parte.Familia ?? "—",
                        OA = parte.OA,
                        JPH = parte.JPH,
                        LineaNombre = linea != null ? (linea.Nombre ?? linea.Codigo) : "—",
                        CeldaCodigo = celda != null ? celda.Codigo : "—",
                        ImagenAyudaVisual = parte.ImagenAyudaVisual,
                        Activo = parte.Activo
                    };

        ViewBag.Lineas = await _context.Lineas
            .Where(l => l.PlantaId == plantaId && l.Activa)
            .Select(l => new { l.Id, Nombre = l.Nombre ?? l.Codigo })
            .ToListAsync();

        var partes = await query.ToListAsync();
        return View(partes);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerParte(int id)
    {
        var p = await _context.NumerosDeParte.FindAsync(id);
        if (p == null) return NotFound();

        return Json(new
        {
            id = p.Id,
            sapPartNumber = p.SapPartNumber,
            noDeParte = p.NoDeParte,
            descripcion = p.Descripcion,
            finalAssembly = p.FinalAssembly,
            familia = p.Familia,
            oa = p.OA,
            jph = p.JPH,
            lineaId = p.LineaId,
            celdaId = p.CeldaId,
            umbralCritico = p.UmbralCritico,
            umbralBajo = p.UmbralBajo,
            umbralAceptable = p.UmbralAceptable
        });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(NumeroDeParte model)
    {
        int plantaId = ObtenerPlantaActivaId();

        if (model.Id == 0)
        {
            // Alta de nueva parte
            model.PlantaId = plantaId;
            model.Activo = true;
            model.CreadoAt = DateTime.UtcNow;
            _context.NumerosDeParte.Add(model);
        }
        else
        {
            // Edición de parte existente
            var dbParte = await _context.NumerosDeParte.FindAsync(model.Id);
            if (dbParte == null) return NotFound();

            dbParte.SapPartNumber = model.SapPartNumber;
            dbParte.NoDeParte = model.NoDeParte;
            dbParte.Descripcion = model.Descripcion;
            dbParte.FinalAssembly = model.FinalAssembly;
            dbParte.Familia = model.Familia;
            dbParte.OA = model.OA;
            dbParte.JPH = model.JPH;
            dbParte.LineaId = model.LineaId;
            dbParte.CeldaId = model.CeldaId;
            dbParte.UmbralCritico = model.UmbralCritico;
            dbParte.UmbralBajo = model.UmbralBajo;
            dbParte.UmbralAceptable = model.UmbralAceptable;
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