using Microsoft.AspNetCore.Mvc;

namespace TurnosLogistica.MVC.Controllers;

public class UsuariosController : Controller
{
    public IActionResult Index() => View();
}