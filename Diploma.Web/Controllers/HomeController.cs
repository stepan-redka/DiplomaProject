using Diploma.Infrastructure.Utils;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Diploma.Web.Models;
using Diploma.Application.Interfaces.Chat;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Diploma.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IRagService _ragService;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ICurrentUserService _currentUserService;

    public HomeController(
        ILogger<HomeController> logger, 
        IRagService ragService, 
        IChatHistoryService chatHistoryService,
        ICurrentUserService currentUserService)
    {
        _ragService = ragService;
        _chatHistoryService = chatHistoryService;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(Guid? sessionId = null)
    {
        try
        {
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            var documents = await _ragService.GetUserDocumentsAsync();
            
            var viewModel = new DashboardViewModel
            {
                Documents = documents,
                TotalDocuments = documents.Count,
                TotalChunks = documents.Sum(d => d.ChunkCount),
                IsAuthenticated = isAuthenticated,
                ActiveSessionId = sessionId,
                UserName = User.Identity?.Name
            };

            if (isAuthenticated)
            {
                viewModel.TotalQueries = await _ragService.GetTotalQueriesAsync();
                var storageBytes = await _ragService.GetStorageUsedAsync();
                viewModel.StorageUsedBytes = storageBytes;
                viewModel.StorageUsedFormatted = StorageFormatter.FormatSize(storageBytes);
                
                // Load sidebar history
                viewModel.RecentSessions = await _chatHistoryService.GetUserSessionsAsync();
                viewModel.TotalSessions = viewModel.RecentSessions.Count;
            }

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard.");
            return View(new DashboardViewModel());
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