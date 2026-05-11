using System.Text.Json;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;

namespace Diploma.Infrastructure.Services;

public class ExportService : IExportService
{
    static ExportService()
    {
        // QuestPDF License setup - Community edition is free for individuals/students
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
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Helvetica));

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

    public string GetExportFileName(string extension)
    {
        return $"Research_Export_{DateTime.Now:yyyyMMdd_HHmm}.{extension.TrimStart('.')}";
    }
}
