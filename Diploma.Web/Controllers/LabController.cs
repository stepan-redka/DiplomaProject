using Microsoft.AspNetCore.Mvc;
using Diploma.Application.Interfaces.Analytics;
using Diploma.Application.Interfaces.Chat;
using Diploma.Application.Interfaces.Documents;
using Microsoft.AspNetCore.Authorization;
using Diploma.Web.Models;
using Diploma.Infrastructure.Utils;
using Diploma.Application.DTOs;

namespace Diploma.Web.Controllers;

[Authorize]
public class LabController : Controller
{
    private readonly IRagService _ragService;
    private readonly IHealthService _healthService;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly IExportService _exportService;

    public LabController(IRagService ragService, IHealthService healthService, IChatHistoryService chatHistoryService, IExportService exportService)
    {
        _ragService = ragService;
        _healthService = healthService;
        _chatHistoryService = chatHistoryService;
        _exportService = exportService;
    }

    public async Task<IActionResult> Benchmarks()
    {
        var sessions = await _chatHistoryService.GetUserSessionsAsync();
        var storageBytes = await _ragService.GetStorageUsedAsync();
        var queries = await _ragService.GetTotalQueriesAsync();
        var docs = await _ragService.GetDocumentCountAsync();
        
        var health = await _healthService.GetSystemHealthAsync();
        var analytics = await _ragService.GetAnalyticsAsync();

        var model = new AnalyticsViewModel
        {
            Health = health,
            TotalSessions = sessions.Count,
            StorageUsedBytes = storageBytes,
            StorageUsedFormatted = StorageFormatter.FormatSize(storageBytes),
            TotalQueries = queries,
            TotalDocuments = docs,
            StorageUsedPercentage = (int)Math.Min(100, (storageBytes / (1024.0 * 1024.0)) / 1024.0 * 100), // Utilization vs 1GB
            
            ModelLatency = analytics.ModelLatency.Select(d => new ModelLatencyDto { ModelName = d.ModelName, AverageLatencyMs = d.AvgLatencyMs }).ToList(),
            IngestionEfficiency = analytics.IngestionEfficiency.Select(d => new IngestionEfficiencyDto { FileSizeBytes = d.FileSizeBytes, ProcessingTimeMs = d.ProcessingTimeMs, FileName = d.FileName }).ToList(),
            SemanticPrecision = analytics.SemanticPrecision.Select(d => new SemanticPrecisionDto { Timestamp = d.Timestamp, MaxScore = d.Score }).ToList(),
            GenerationThroughput = analytics.GenerationThroughput.Select(d => new ThroughputDto { ModelName = d.ModelName, TokensPerSec = d.TokensPerSec }).ToList(),
            MathHumanCorrelation = analytics.MathHumanCorrelation.Select(d => new CorrelationDto { SimilarityScore = d.Score, UserEffectiveness = d.Feedback }).ToList(),
            KnowledgeDensity = analytics.KnowledgeDensity.Select(d => new KnowledgeDensityDto { DocumentName = d.DocName, ChunkCount = d.Chunks }).ToList()
        };

        return PartialView("_Benchmarks", model);
    }

    public async Task<IActionResult> Health()
    {
        var health = await _healthService.GetSystemHealthAsync();
        return PartialView("_Health", health);
    }

    public async Task<IActionResult> ExportCsv()
    {
        try
        {
            // Fetch last 50 messages for analytics export
            var history = await _ragService.GetChatHistoryAsync(limit: 100);
            var csvBytes = _exportService.ExportResearchDataAsCsv(history);
            var fileName = _exportService.GetExportFileName("csv");

            return File(csvBytes, "text/csv", fileName);
        }
        catch (Exception)
        {
            return StatusCode(500, "Error exporting research data.");
        }
    }
}
