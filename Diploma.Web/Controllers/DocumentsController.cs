using Microsoft.AspNetCore.Mvc;
using Diploma.Application.Interfaces;
using Diploma.Application.DTOs;

namespace Diploma.Web.Controllers;

public class DocumentsController : Controller
{
    private readonly IRagService _ragService;
    private readonly IDocumentParsingService _documentParsingService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IRagService ragService, 
        IDocumentParsingService documentParsingService,
        ILogger<DocumentsController> logger)
    {
        _ragService = ragService;
        _documentParsingService = documentParsingService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var documents = await _ragService.GetUserDocumentsAsync();
        return View(documents);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file selected for upload.");
            return BadRequest("Please select a file to upload.");
        }

        try
        {
            using var stream = file.OpenReadStream();
            var parsedDoc = await _documentParsingService.ParseDocumentAsync(stream, file.FileName);

            if (string.IsNullOrWhiteSpace(parsedDoc.Content))
            {
                _logger.LogWarning("Document contains no readable text: {FileName}", file.FileName);
                return BadRequest("The document contains no readable text.");
            }

            var response = await _ragService.IngestDocumentAsync(parsedDoc.Content, file.FileName);

            if (response.Success)
            {
                _logger.LogInformation("Successfully ingested document: {FileName}", file.FileName);
                return RedirectToAction(nameof(Index));
            }

            return StatusCode(500, $"Indexing failed: {response.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing upload for {FileName}", file.FileName);
            return StatusCode(500, "An error occurred while processing the file.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearCollection()
    {
        var result = await _ragService.ClearCollectionAsync();
        if (result)
        {
            _logger.LogInformation("Successfully cleared document collection.");
            return RedirectToAction(nameof(Index));
        }
        return StatusCode(500, "Failed to clear collection.");
    }
}