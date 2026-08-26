using Microsoft.AspNetCore.Mvc;

namespace TurnosLogistica.MVC.Controllers;

public class EstructuraController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Gestión de Estructura";
        return View();
    }
}