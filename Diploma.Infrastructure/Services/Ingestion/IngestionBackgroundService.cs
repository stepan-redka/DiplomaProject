using Diploma.Application.Interfaces.Chat;
using Diploma.Application.Interfaces.Documents;
using Diploma.Application.Interfaces.Identity;
using Diploma.Infrastructure.Services.Chat;
using Diploma.Infrastructure.Services.Documents;
using Diploma.Infrastructure.Services.Identity;
using Diploma.Infrastructure.Services.Ingestion;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Diploma.Infrastructure.Services.Ingestion;

/// <summary>
/// Background worker that consumes ingestion tasks from the channel.
/// Decouples file processing from the web request.
/// </summary>
public class IngestionBackgroundService : BackgroundService
{
    private readonly IngestionChannel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionBackgroundService> _logger;

    public IngestionBackgroundService(
        IngestionChannel channel,
        IServiceProvider serviceProvider,
        ILogger<IngestionBackgroundService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingestion Background Service started.");

        await foreach (var task in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            Guid documentId = Guid.NewGuid();
            try
            {
                _logger.LogInformation("Background processing started for: {FileName} (User: {UserId})", task.FileName, task.UserId);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1. Set the background user context FIRST (Critical for Multi-tenant SaveChanges)
                var userService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>() as CurrentUserService;
                userService?.SetManualUserId(task.UserId);

                // 2. Create Initial Pending Record
                var document = new Document
                {
                    Id = documentId,
                    UserId = task.UserId,
                    FileName = task.FileName,
                    Status = IngestionStatus.Processing,
                    CreatedAt = DateTime.UtcNow,
                    Content = "[Processing...]",
                    FileSizeBytes = task.FileData.LongLength
                };
                dbContext.Documents.Add(document);
                await dbContext.SaveChangesAsync(stoppingToken);

                var parsingService = scope.ServiceProvider.GetRequiredService<IDocumentParsingService>();
                var ragService = scope.ServiceProvider.GetRequiredService<IRagService>();

                // 3. Parse the document bytes
                using var stream = new MemoryStream(task.FileData);
                var parsedDoc = await parsingService.ParseDocumentAsync(stream, task.FileName, stoppingToken);

                if (parsedDoc.Success && !string.IsNullOrWhiteSpace(parsedDoc.Content))
                {
                    // 4. Ingest into RAG pipeline (Pass existing documentId to prevent duplication)
                    await ragService.IngestDocumentAsync(parsedDoc.Content, task.FileName, documentId, stoppingToken);

                    _logger.LogInformation("Successfully background-indexed: {FileName}", task.FileName);
                }
                else
                {
                    throw new Exception(parsedDoc.ErrorMessage ?? "Unknown parsing error.");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error processing background task for {FileName}", task.FileName);

                // Persist failure to DB for Admin Telemetry
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var doc = await dbContext.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == documentId);
                if (doc != null)
                {
                    doc.Status = IngestionStatus.Failed;
                    doc.ErrorMessage = ex.Message;
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
        }

        _logger.LogInformation("Ingestion Background Service stopped.");
    }
}
