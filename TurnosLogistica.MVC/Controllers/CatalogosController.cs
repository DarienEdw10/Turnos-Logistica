using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnosLogistica.Domain.Data;
using TurnosLogistica.Domain.Models; // Aquí viven tus entidades (TurnoParo, etc.)
using TurnosLogistica.MVC.Models;    // Aquí vive ParoTemporalCatalogoViewModel

namespace TurnosLogistica.MVC.Controllers
{
    public class CatalogosController : Controller
    {
        private readonly AppDbContext _context;

        public CatalogosController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Vista del Catálogo de Paros Temporales (Categoría 2 y ProgramacionId NULL)
        [HttpGet]
        public async Task<IActionResult> ParosTemporales()
        {
            var lista = await _context.TurnoParos
                .AsNoTracking()
                .Where(tp => tp.CategoriaParo == 2 && tp.ProgramacionId == null)
                .OrderBy(tp => tp.TipoParo)
                .Select(tp => new ParoTemporalCatalogoViewModel
                {
                    Id = tp.Id,
                    TipoParo = tp.TipoParo,
                    Descripcion = tp.Descripcion ?? tp.TipoParo,
                    DuracionMinutos = tp.DuracionMinutos,
                    Activo = tp.Activo
                })
                .ToListAsync();

            return View(lista);
        }

        // 2. Guardar o actualizar plantilla de paro temporal en TurnoParos
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarParoTemporal(int id, string tipoParo, string descripcion, int duracionMinutos, bool activo)
        {
            if (string.IsNullOrWhiteSpace(tipoParo))
            {
                TempData["Error"] = "El tipo o motivo del paro es obligatorio.";
                return RedirectToAction(nameof(ParosTemporales));
            }

            if (id == 0)
            {
                var nuevo = new TurnoParo
                {
                    TurnoId = null,
                    ProgramacionId = null,
                    TipoParo = tipoParo.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(descripcion) ? tipoParo.Trim() : descripcion.Trim(),
                    DuracionMinutos = duracionMinutos > 0 ? duracionMinutos : 15,
                    EsProgramado = true,
                    CategoriaParo = 2, // 2 = Temporal / Día
                    Activo = activo
                };
                _context.TurnoParos.Add(nuevo);
            }
            else
            {
                var existente = await _context.TurnoParos.FindAsync(id);
                if (existente != null)
                {
                    existente.TipoParo = tipoParo.Trim();
                    existente.Descripcion = string.IsNullOrWhiteSpace(descripcion) ? tipoParo.Trim() : descripcion.Trim();
                    existente.DuracionMinutos = duracionMinutos > 0 ? duracionMinutos : 15;
                    existente.Activo = activo;
                    existente.CategoriaParo = 2;
                    _context.Update(existente);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Paro temporal guardado exitosamente en el catálogo.";
            return RedirectToAction(nameof(ParosTemporales));
        }

        // 3. Endpoint AJAX consumido por el Formulario de Programación
        [HttpGet]
        public async Task<IActionResult> ObtenerParosTemporalesActivos()
        {
            var catalogo = await _context.TurnoParos
                .AsNoTracking()
                .Where(tp => tp.Activo && tp.CategoriaParo == 2 && tp.ProgramacionId == null)
                .Select(tp => new {
                    id = tp.Id,
                    tipoParo = tp.TipoParo,
                    descripcion = tp.Descripcion ?? tp.TipoParo,
                    duracionMinutos = tp.DuracionMinutos
                })
                .ToListAsync();

            return Json(catalogo);
        }
        // 4. Endpoint AJAX consumido por la Gestión de Turnos para el Catálogo Global de Paros Programados
        [HttpGet]
        public async Task<IActionResult> ObtenerParosProgramadosActivos()
        {
            var catalogo = await _context.TurnoParos
                .AsNoTracking()
                .Where(tp => tp.Activo && tp.CategoriaParo == 1 && tp.ProgramacionId == null)
                .Select(tp => new {
                    id = tp.Id,
                    tipoParo = tp.TipoParo,
                    descripcion = tp.Descripcion ?? tp.TipoParo,
                    duracionMinutos = tp.DuracionMinutos
                })
                .ToListAsync();

            return Json(catalogo);
        }
    }
    
}