using Microsoft.AspNetCore.Mvc;
using Diploma.Application.Interfaces.Chat;
using Diploma.Application.Interfaces.Documents;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.DTOs;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace Diploma.Web.Controllers;

public class ChatController : Controller
{
    private readonly ILogger<ChatController> _logger;
    private readonly IRagService _ragService;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExportService _exportService;

    public ChatController(
        ILogger<ChatController> logger, 
        IRagService ragService, 
        IChatHistoryService chatHistoryService,
        ICurrentUserService currentUserService,
        IExportService exportService)
    {
        _logger = logger;
        _ragService = ragService;
        _chatHistoryService = chatHistoryService;
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
    public async Task<IActionResult> GetSessionHistory(Guid sessionId)
    {
        if (!_currentUserService.IsAuthenticated) return Unauthorized();

        try
        {
            var session = await _chatHistoryService.GetSessionDetailsAsync(sessionId);
            if (session == null) return NotFound();

            return Json(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session history.");
            return StatusCode(500, "Unable to load history.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        if (!_currentUserService.IsAuthenticated) return Unauthorized();

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
        _logger.LogInformation("Processing RAG query (User: {UserId}). Session: {SessionId}, Model: {Model}, Intent: {Intent}, HighFidelity: {HiFi}", 
            _currentUserService.UserId, request.SessionId, request.SelectedModel ?? "Default", request.Intent, request.IsHighFidelity);

        try
        {
            var result = await _ragService.QueryAsync(
                request.Question, 
                request.SessionId, 
                request.TopK, 
                request.Intent, 
                request.SelectedModel,
                request.IsHighFidelity);
            sw.Stop();

            return Json(new
            {
                messageId = result.MessageId,
                sessionId = result.SessionId,
                sessionTitle = result.SessionTitle,
                answer = result.Answer,
                sources = result.Sources,
                latencyMs = sw.ElapsedMilliseconds,
                isAuthenticated = _currentUserService.IsAuthenticated
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to process RAG query.");
            return StatusCode(500, "An error occurred while communicating with the AI service.");
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var result = await _chatHistoryService.DeleteSessionAsync(id);
        if (result)
        {
            return Ok();
        }
        return BadRequest("Failed to delete session.");
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
    public async Task<IActionResult> ExportSession(Guid sessionId, string format = "pdf")
    {
        if (!_currentUserService.IsAuthenticated) return Unauthorized();

        if (sessionId == Guid.Empty)
        {
            return BadRequest("A valid session must be selected to export findings.");
        }

        try
        {
            var session = await _chatHistoryService.GetSessionDetailsAsync(sessionId);
            if (session == null) return NotFound("The requested research session could not be found.");

            if (session.Messages == null || !session.Messages.Any(m => m.Content.Length >= 15))
            {
                return BadRequest("This research thread is currently empty or contains insufficient data for synthesis. Please conduct further research before exporting.");
            }

            byte[] fileBytes;
            string contentType;
            string fileName;

            if (format.ToLower() == "json")
            {
                fileBytes = _exportService.ExportChatHistoryAsJson(session.Messages);
                contentType = "application/json";
                fileName = _exportService.GetExportFileName("json", session.Title);
            }
            else
            {
                var userEmail = User.Identity?.Name ?? "Researcher";
                fileBytes = _exportService.ExportSessionAsPdf(session, userEmail);
                contentType = "application/pdf";
                fileName = _exportService.GetExportFileName("pdf", session.Title);
            }

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting chat session: {SessionId}", sessionId);
            return StatusCode(500, "An internal error occurred during synthesis generation.");
        }
    }
}
