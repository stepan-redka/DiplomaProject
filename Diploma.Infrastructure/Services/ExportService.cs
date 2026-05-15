using System.Text.Json;
using System.Text;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Diploma.Infrastructure.Services;

public class ExportService : IExportService
{
    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ExportChatHistoryAsJson(IEnumerable<ChatMessageDto> history)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.SerializeToUtf8Bytes(history, options);
    }

    public byte[] ExportChatHistoryAsPdf(IEnumerable<ChatMessageDto> history, string userName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica"));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("RagSystem Research Report").FontSize(20).SemiBold().FontColor(Colors.Indigo.Medium);
                        col.Item().Text($"{DateTime.Now:MMMM dd, yyyy}").FontSize(10).FontColor(Colors.Grey.Medium);
                    });

                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().Text($"Researcher: {userName}").FontSize(10).SemiBold();
                        col.Item().Text($"Session Export").FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                });

                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(15);

                    foreach (var msg in history.OrderBy(m => m.CreatedAt))
                    {
                        column.Item().Row(row =>
                        {
                            row.Spacing(10);
                            
                            var isAi = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);
                            
                            row.AutoItem().Width(60).AlignRight().Text(isAi ? "AI" : "YOU")
                                .FontSize(9).SemiBold().FontColor(isAi ? Colors.Indigo.Medium : Colors.Black);

                            row.RelativeItem().Column(msgCol =>
                            {
                                msgCol.Item().PaddingBottom(5).Text(msg.Content).LineHeight(1.5f);
                                msgCol.Item().AlignRight().Text($"{msg.CreatedAt:HH:mm:ss}").FontSize(8).FontColor(Colors.Grey.Medium);
                                
                                if (isAi && msg.Effectiveness > 0)
                                {
                                    var label = msg.Effectiveness == 1 ? "Positive Feedback" : "Negative Feedback";
                                    var color = msg.Effectiveness == 1 ? Colors.Green.Medium : Colors.Red.Medium;
                                    msgCol.Item().Text(label).FontSize(8).FontColor(color).Italic();
                                }
                            });
                        });
                        
                        column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportResearchDataAsCsv(IEnumerable<ChatMessageDto> history)
    {
        var sb = new StringBuilder();
        // Header
        sb.AppendLine("Timestamp,ModelName,LatencyMs,SimilarityScore,UserFeedback,TokenCount");

        foreach (var msg in history.Where(m => m.Role == "assistant"))
        {
            double maxScore = 0;
            if (msg.Sources.Any())
            {
                maxScore = msg.Sources.Max(s => s.Score);
            }

            var line = $"{msg.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                       $"\"{msg.ModelName ?? "Unknown"}\"," +
                       $"{msg.ProcessingTimeMs:F2}," +
                       $"{maxScore:F4}," +
                       $"{(int)msg.Effectiveness}," +
                       $"{msg.TokenCount}";
            
            sb.AppendLine(line);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public string GetExportFileName(string extension)
    {
        return $"Research_Export_{DateTime.Now:yyyyMMdd_HHmm}.{extension.TrimStart('.')}";
    }
}
