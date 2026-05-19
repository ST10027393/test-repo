// FILE: TechMove_GLMS/Controllers/HomeController.cs
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TechMove_GLMS.Models;

namespace TechMove_GLMS.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("currentUser"))) 
        {
            return RedirectToAction("Login", "Auth");
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
