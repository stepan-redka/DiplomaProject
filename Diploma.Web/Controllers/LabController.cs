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
            StorageUsedPercentage = (int)Math.Min(100, storageBytes / (1024.0 * 1024.0 * 1024.0) * 100), // Utilization vs 1GB

            ModelLatency = analytics.ModelLatency,
            IngestionEfficiency = analytics.IngestionEfficiency,
            RetrievalSimilarity = analytics.RetrievalSimilarity,
            GenerationThroughput = analytics.GenerationThroughput,
            KnowledgeDensity = analytics.KnowledgeDensity,
            StorageFootprint = analytics.StorageFootprint
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
