using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Diploma.Web.Models;
using Diploma.Application.Interfaces;
using Diploma.Application.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Diploma.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IRagService _ragService;
    private readonly ICurrentUserService _currentUserService;

    public HomeController(ILogger<HomeController> logger, IRagService ragService, ICurrentUserService currentUserService)
    {
        _ragService = ragService;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        try
        {
            // Even guests can see 'public' documents thanks to Global Query Filters
            var documents = await _ragService.GetUserDocumentsAsync();
            return View(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard documents.");
            return View(new List<DocumentDto>());
        }
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> Status()
    {
        try 
        {
            int count = await _ragService.GetDocumentCountAsync();
            return Content($"Collection contains {count} accessible documents.");
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