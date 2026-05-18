using Microsoft.AspNetCore.Mvc;
using Diploma.Application.Interfaces.Chat;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Diploma.Infrastructure.Services.Chat;
using Diploma.Infrastructure.Services.Identity;
using Diploma.Infrastructure.Services.Ingestion;
using Microsoft.IO;

namespace Diploma.Web.Controllers;

[Authorize]
public class DocumentsController : Controller
{
    private readonly IRagService _ragService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IngestionChannel _ingestionChannel;
    private readonly RecyclableMemoryStreamManager _streamManager;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IRagService ragService, 
        ICurrentUserService currentUserService,
        IngestionChannel ingestionChannel,
        RecyclableMemoryStreamManager streamManager,
        ILogger<DocumentsController> logger)
    {
        _ragService = ragService;
        _currentUserService = currentUserService;
        _ingestionChannel = ingestionChannel;
        _streamManager = streamManager;
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

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        int queuedCount = 0;
        foreach (var file in files)
        {
            try
            {
                // PERFORMANCE: Use RecyclableMemoryStream to avoid LOH fragmentation
                using var pooledStream = _streamManager.GetStream();
                await file.CopyToAsync(pooledStream);
                var fileData = pooledStream.ToArray();

                var task = new IngestionTask(fileData, file.FileName, userId);
                await _ingestionChannel.Writer.WriteAsync(task);
                
                _logger.LogInformation("File {FileName} queued for background ingestion.", file.FileName);
                queuedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing file {FileName}", file.FileName);
            }
        }

        // Return 202 Accepted for high-load responsiveness
        return Accepted(new { message = $"Successfully queued {queuedCount} files for processing." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasteText([FromBody] IngestRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Content cannot be empty.");
        }

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var docName = string.IsNullOrWhiteSpace(request.DocumentName) 
            ? $"Manual_Entry_{DateTime.Now:yyyyMMdd_HHmm}.txt" 
            : request.DocumentName;

        try
        {
            var fileData = System.Text.Encoding.UTF8.GetBytes(request.Content);
            var task = new IngestionTask(fileData, docName, userId);
            await _ingestionChannel.Writer.WriteAsync(task);

            return Accepted(new { message = "Text queued for indexing." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing pasted text.");
            return StatusCode(500, "An error occurred while queuing the text.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _ragService.DeleteDocumentAsync(id);
        if (result)
        {
            return Ok();
        }
        return BadRequest("Failed to delete document.");
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
