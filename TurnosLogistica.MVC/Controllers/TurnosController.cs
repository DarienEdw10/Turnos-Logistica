using Microsoft.AspNetCore.Mvc;

namespace TurnosLogistica.MVC.Controllers;

public class TurnosController : Controller
{
    public IActionResult Index() => View();
}