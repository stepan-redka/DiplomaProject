using Microsoft.AspNetCore.Mvc;
using Diploma.Application.Interfaces;
using Diploma.Application.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Diploma.Web.Controllers;

[Authorize]
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
    public async Task<IActionResult> Upload(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            _logger.LogWarning("No files selected for upload.");
            return BadRequest("Please select at least one file to upload.");
        }

        int successCount = 0;
        List<string> errors = new();

        foreach (var file in files)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var parsedDoc = await _documentParsingService.ParseDocumentAsync(stream, file.FileName);

                if (string.IsNullOrWhiteSpace(parsedDoc.Content))
                {
                    errors.Add($"{file.FileName}: Document contains no readable text.");
                    continue;
                }

                var response = await _ragService.IngestDocumentAsync(parsedDoc.Content, file.FileName);

                if (response.Success)
                {
                    successCount++;
                }
                else
                {
                    errors.Add($"{file.FileName}: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing upload for {FileName}", file.FileName);
                errors.Add($"{file.FileName}: Internal error.");
            }
        }

        if (errors.Any())
        {
            var message = $"Processed {successCount} files. Errors: {string.Join(" | ", errors)}";
            return successCount > 0 ? Ok(message) : BadRequest(message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasteText([FromBody] IngestRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Content cannot be empty.");
        }

        var docName = string.IsNullOrWhiteSpace(request.DocumentName) 
            ? $"Manual_Entry_{DateTime.Now:yyyyMMdd_HHmm}" 
            : request.DocumentName;

        try
        {
            var response = await _ragService.IngestDocumentAsync(request.Content, docName);
            if (response.Success)
            {
                return Ok(new { message = "Text indexed successfully", chunks = response.ChunksCreated });
            }
            return StatusCode(500, $"Indexing failed: {response.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pasted text.");
            return StatusCode(500, "An error occurred while processing the text.");
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