using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Diploma.Web.Models;
using Diploma.Application.Interfaces;

namespace Diploma.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IRagService _ragService;

    public HomeController(ILogger<HomeController> logger, IRagService ragService)
    {
        _ragService = ragService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        try 
        {
            int count = await _ragService.GetDocumentCountAsync();
            return Content($"You have {count} documents in your collection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document count");
            return Content("Unable to retrieve status at this time.");
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}