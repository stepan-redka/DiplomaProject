using Microsoft.AspNetCore.Mvc;
using Diploma.Application.Interfaces;
using Diploma.Application.DTOs;
using System.Diagnostics;

namespace Diploma.Web.Controllers;

public class ChatController : Controller
{
    private readonly ILogger<ChatController> _logger;
    private readonly IRagService _ragService;

    public ChatController(ILogger<ChatController> logger, IRagService ragService)
    {
        _logger = logger;
        _ragService = ragService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        // For MVC, we usually return the view for the chat interface.
        // If we want a pure API welcome message, Json is fine, but Index usually means UI.
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] QueryRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Question))
        {
            _logger.LogWarning("Received empty or null chat request.");
            return BadRequest("Please enter a question.");
        }

        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Processing RAG query. Length: {CharCount}", request.Question.Length);

        try
        {
            var result = await _ragService.QueryAsync(request.Question);
            sw.Stop();

            _logger.LogInformation("Query processed successfully in {ElapsedMs}ms", sw.ElapsedMilliseconds);

            return Json(new
            {
                answer = result.Answer,
                sources = result.Sources,
                latencyMs = sw.ElapsedMilliseconds
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
}