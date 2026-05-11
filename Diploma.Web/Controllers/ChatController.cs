using Microsoft.AspNetCore.Mvc;
using Diploma.Application.Interfaces;
using Diploma.Application.DTOs;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace Diploma.Web.Controllers;

public class ChatController : Controller
{
    private readonly ILogger<ChatController> _logger;
    private readonly IRagService _ragService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExportService _exportService;

    public ChatController(
        ILogger<ChatController> logger, 
        IRagService ragService, 
        ICurrentUserService currentUserService,
        IExportService exportService)
    {
        _logger = logger;
        _ragService = ragService;
        _currentUserService = currentUserService;
        _exportService = exportService;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        // History is only for authenticated users (Identity Upsell trigger point)
        if (!_currentUserService.IsAuthenticated)
        {
            return Unauthorized();
        }

        try
        {
            var history = await _ragService.GetChatHistoryAsync();
            return Json(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history.");
            return StatusCode(500, "Unable to load chat history.");
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Ask([FromBody] QueryRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Question))
        {
            _logger.LogWarning("Received empty or null chat request.");
            return BadRequest("Please enter a question.");
        }

        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Processing RAG query (User: {UserId}). Length: {CharCount}", 
            _currentUserService.UserId, request.Question.Length);

        try
        {
            var result = await _ragService.QueryAsync(request.Question, request.TopK);
            sw.Stop();

            _logger.LogInformation("Query processed successfully in {ElapsedMs}ms", sw.ElapsedMilliseconds);

            return Json(new
            {
                messageId = result.MessageId,
                answer = result.Answer,
                sources = result.Sources,
                latencyMs = sw.ElapsedMilliseconds,
                isAuthenticated = _currentUserService.IsAuthenticated
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to process RAG query after {ElapsedMs}ms. Question: {Question}", 
                sw.ElapsedMilliseconds, request.Question);
            return StatusCode(500, "An error occurred while communicating with the AI service.");
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> SetFeedback([FromBody] SetFeedbackRequest request)
    {
        if (request == null || request.MessageId == Guid.Empty)
        {
            return BadRequest("Invalid feedback request.");
        }

        try
        {
            var success = await _ragService.SetFeedbackAsync(request.MessageId, request.Effectiveness);
            return success ? Ok() : NotFound("Message not found or unauthorized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting message feedback.");
            return StatusCode(500, "An error occurred while saving feedback.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportSession(string format = "pdf")
    {
        if (!_currentUserService.IsAuthenticated) return Unauthorized();

        try
        {
            var history = await _ragService.GetChatHistoryAsync(limit: 1000);
            
            byte[] fileBytes;
            string contentType;
            string fileName;

            if (format.ToLower() == "json")
            {
                fileBytes = _exportService.ExportChatHistoryAsJson(history);
                contentType = "application/json";
                fileName = _exportService.GetExportFileName("json");
            }
            else
            {
                var userName = User.Identity?.Name ?? "Researcher";
                fileBytes = _exportService.ExportChatHistoryAsPdf(history, userName);
                contentType = "application/pdf";
                fileName = _exportService.GetExportFileName("pdf");
            }

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting chat session.");
            return StatusCode(500, "An error occurred during export.");
        }
    }
}