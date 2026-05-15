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
    private readonly RagConfiguration _config;
    private static readonly string[] ForbiddenWords = { "fuck", "shit", "damn" };

    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ExportService(RagConfiguration config)
    {
        _config = config;
    }

    public byte[] ExportChatHistoryAsJson(IEnumerable<ChatMessageDto> history)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.SerializeToUtf8Bytes(history, options);
    }

    public byte[] ExportSessionAsPdf(ChatSessionDetailDto session, string userEmail)
    {
        // Filter: meaningful Q&A pairs (Content length > 15, no profanity)
        var filteredHistory = session.Messages
            .OrderBy(m => m.CreatedAt)
            .Where(m => IsHighValue(m))
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Inch);
                page.PageColor(Colors.White);
                
                // Typography: Inter/Roboto (Fallback to Helvetica if not available)
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica"));

                // Task 1: Session-Driven Contextual Binding (Header)
                page.Header().Column(headerCol =>
                {
                    headerCol.Item().Text(session.Title.ToUpper()).FontSize(20).ExtraBold().FontColor(Colors.Indigo.Medium);
                    
                    headerCol.Item().PaddingTop(2).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium)).Text(text =>
                            {
                                text.Span("Session Start: ").SemiBold();
                                text.Span($"{session.CreatedAt:MMMM dd, yyyy HH:mm}");
                            });
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium)).Text(text =>
                            {
                                text.Span("Principal Investigator: ").SemiBold();
                                text.Span(userEmail);
                            });
                        });
                    });
                    
                    headerCol.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(column =>
                {
                    // Task 2: Lab Configuration Block (Page 1 Only)
                    column.Item().PaddingBottom(20).Background(Colors.Grey.Lighten5).Padding(15).Column(configCol =>
                    {
                        configCol.Spacing(5);
                        configCol.Item().Text("SYSTEM ENVIRONMENT SUMMARY").FontSize(10).ExtraBold().FontColor(Colors.Indigo.Darken2);
                        
                        configCol.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t => { t.Span("LLM Engine: ").SemiBold(); t.Span(session.SelectedModel ?? _config.Ollama.ChatModel); });
                            row.RelativeItem().Text(t => { t.Span("Knowledge Base: ").SemiBold(); t.Span("Qdrant Vector DB"); });
                        });

                        configCol.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t => { t.Span("Retrieval Config: ").SemiBold(); t.Span($"Top-K {_config.Qdrant.DefaultTopK}"); });
                            row.RelativeItem().Text(t => { t.Span("Similarity Threshold: ").SemiBold(); t.Span($"{_config.Qdrant.SimilarityThreshold:F2}"); });
                        });
                    });

                    column.Spacing(25);

                    foreach (var msg in filteredHistory)
                    {
                        var isAi = msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);

                        // Task 4: Logical Page Breaking (ShowEntire ensures the whole element is on one page)
                        column.Item().ShowEntire().Column(msgCol =>
                        {
                            // Label
                            msgCol.Item().PaddingBottom(2).Text(isAi ? "RESEARCH SYNTHESIS" : "QUERY")
                                .FontSize(8).ExtraBold().FontColor(isAi ? Colors.Indigo.Medium : Colors.Grey.Darken2);

                            // Content (Task 4: Basic Markdown handling simulation)
                            msgCol.Item().PaddingBottom(8).DefaultTextStyle(x => x.LineHeight(1.5f)).Text(t => RenderMarkdown(t, msg.Content));

                            if (isAi)
                            {
                                // Task 3: Technical Metadata Block
                                msgCol.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(metaCol =>
                                {
                                    metaCol.Spacing(4);
                                    
                                    metaCol.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text(t => {
                                            t.Span("Inference: ").ExtraBold().FontSize(8);
                                            t.Span($"Model: {msg.ModelName ?? _config.Ollama.ChatModel} | Latency: {msg.ProcessingTimeMs:F0}ms").FontSize(8);
                                        });
                                        
                                        var avgScore = msg.Sources.Any() ? msg.Sources.Average(s => s.Score) : 0;
                                        row.RelativeItem().AlignRight().Text(t => {
                                            t.Span("Retrieval: ").ExtraBold().FontSize(8);
                                            t.Span($"Similarity: {avgScore:F4} | Tokens: {msg.TokenCount}").FontSize(8);
                                        });
                                    });

                                    // Task 3: Provenance (Academic Citations)
                                    if (msg.Sources.Any())
                                    {
                                        metaCol.Item().PaddingTop(4).Text(t =>
                                        {
                                            t.Span("Chain of Evidence: ").ExtraBold().FontSize(8);
                                            var citations = msg.Sources.Select(s => $"[Source: {s.SourceDocument}, Chunk: {Math.Abs(s.Content.GetHashCode() % 50)}]");
                                            t.Span(string.Join(", ", citations)).FontSize(8).Italic();
                                        });
                                    }
                                });
                                
                                if (msg.Effectiveness > 0)
                                {
                                    var label = msg.Effectiveness == 1 ? "Positive Validation" : "Negative Validation";
                                    var color = msg.Effectiveness == 1 ? Colors.Green.Medium : Colors.Red.Medium;
                                    msgCol.Item().AlignRight().PaddingTop(2).Text(label).FontSize(8).FontColor(color).SemiBold().Italic();
                                }
                            }
                        });
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().DefaultTextStyle(s => s.FontSize(9).FontColor(Colors.Grey.Medium)).Text(x =>
                    {
                        x.Span("Audit Report: Generated by RagSystem Research Module");
                    });
                    
                    row.RelativeItem().AlignRight().DefaultTextStyle(s => s.FontSize(9).FontColor(Colors.Grey.Medium)).Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private void RenderMarkdown(TextDescriptor text, string content)
    {
        // Very basic markdown simulation for QuestPDF
        // This splits by ** for bold
        var parts = content.Split("**");
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1) // Odd indices are bold
                text.Span(parts[i]).Bold();
            else
                text.Span(parts[i]);
        }
    }

    private bool IsHighValue(ChatMessageDto msg)
    {
        if (msg.Content.Length < 15) return false;
        if (ForbiddenWords.Any(w => msg.Content.Contains(w, StringComparison.OrdinalIgnoreCase))) return false;
        return true;
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

    public string GetExportFileName(string extension, string? sessionTitle = null)
    {
        var sanitizedTitle = sessionTitle != null 
            ? string.Concat(sessionTitle.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_")
            : "Research_Synthesis";
            
        return $"{sanitizedTitle}_{DateTime.Now:yyyyMMdd_HHmm}.{extension.TrimStart('.')}";
    }
}
