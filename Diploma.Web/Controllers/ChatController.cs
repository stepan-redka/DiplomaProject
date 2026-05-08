using Microsoft.AspNetCore.Mvc;
using Diploma.Application.Interfaces;
using Diploma.Application.DTOs;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace Diploma.Web.Controllers;

[Authorize]
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
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
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