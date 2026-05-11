using Diploma.Application.Interfaces;
using Diploma.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Services;

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

        // ReadAllAsync handles the CancellationToken and yields when the channel is empty
        await foreach (var task in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Background processing started for: {FileName} (User: {UserId})", task.FileName, task.UserId);
                
                using var scope = _serviceProvider.CreateScope();
                
                // 1. Set the background user context for multi-tenancy
                var userService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>() as CurrentUserService;
                userService?.SetManualUserId(task.UserId);

                var parsingService = scope.ServiceProvider.GetRequiredService<IDocumentParsingService>();
                var ragService = scope.ServiceProvider.GetRequiredService<IRagService>();

                // 2. Parse the document bytes
                using var stream = new MemoryStream(task.FileData);
                var parsedDoc = await parsingService.ParseDocumentAsync(stream, task.FileName, stoppingToken);

                if (parsedDoc.Success && !string.IsNullOrWhiteSpace(parsedDoc.Content))
                {
                    // 3. Ingest into RAG pipeline
                    await ragService.IngestDocumentAsync(parsedDoc.Content, task.FileName, stoppingToken);
                    _logger.LogInformation("Successfully background-indexed: {FileName}", task.FileName);
                }
                else
                {
                    _logger.LogWarning("Background parsing failed for {FileName}: {Error}", task.FileName, parsedDoc.ErrorMessage);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error processing background task for {FileName}", task.FileName);
            }
        }

        _logger.LogInformation("Ingestion Background Service stopped.");
    }
}
