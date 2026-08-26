using Microsoft.AspNetCore.Mvc;

namespace TurnosLogistica.MVC.Controllers;

public class AuditoriaController : Controller
{
    public IActionResult Index(DateTime? desde, DateTime? hasta) => View();
}