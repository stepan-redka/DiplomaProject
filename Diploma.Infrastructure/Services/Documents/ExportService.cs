using System.Text.Json;
using System.Text;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Diploma.Infrastructure.Services.Documents;

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
        var orderedMessages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .Where(m => IsHighValue(m))
            .ToList();

        var researchUnits = BuildResearchUnits(orderedMessages);
        var assistantMessages = researchUnits.Select(u => u.Answer).ToList();
        var sourceCount = assistantMessages.Sum(m => m.Sources.Count);
        var averageLatency = assistantMessages.Any() ? assistantMessages.Average(m => m.ProcessingTimeMs) : 0;
        var averageSimilarity = assistantMessages.SelectMany(m => m.Sources).Any()
            ? assistantMessages.SelectMany(m => m.Sources).Average(s => s.Score)
            : 0;
        var totalTokens = assistantMessages.Sum(m => m.TokenCount);

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
                        configCol.Item().Text("RESEARCH EXPORT SUMMARY").FontSize(10).ExtraBold().FontColor(Colors.Indigo.Darken2);

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

                        configCol.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t => { t.Span("Research Units: ").SemiBold(); t.Span(researchUnits.Count.ToString()); });
                            row.RelativeItem().Text(t => { t.Span("Evidence Citations: ").SemiBold(); t.Span(sourceCount.ToString()); });
                        });

                        configCol.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t => { t.Span("Avg. Latency: ").SemiBold(); t.Span($"{averageLatency:F0} ms"); });
                            row.RelativeItem().Text(t => { t.Span("Avg. Similarity: ").SemiBold(); t.Span($"{averageSimilarity:F4}"); });
                        });

                        configCol.Item().Text(t =>
                        {
                            t.Span("Total Generated Tokens: ").SemiBold();
                            t.Span(totalTokens.ToString());
                        });
                    });

                    column.Spacing(25);

                    var unitIndex = 1;
                    foreach (var unit in researchUnits)
                    {
                        column.Item().ShowEntire().Column(unitCol =>
                        {
                            unitCol.Spacing(8);

                            unitCol.Item().Text($"RESEARCH UNIT {unitIndex:00}")
                                .FontSize(9).ExtraBold().FontColor(Colors.Indigo.Medium);

                            if (unit.Query != null)
                            {
                                unitCol.Item().Background(Colors.Grey.Lighten5).Padding(10).Column(queryCol =>
                                {
                                    queryCol.Spacing(3);
                                    queryCol.Item().Text("Research Question").FontSize(8).ExtraBold().FontColor(Colors.Grey.Darken2);
                                    queryCol.Item().DefaultTextStyle(x => x.LineHeight(1.4f)).Text(t => RenderMarkdown(t, unit.Query.Content));
                                });
                            }

                            unitCol.Item().Column(answerCol =>
                            {
                                answerCol.Spacing(4);
                                answerCol.Item().Text("Synthesized Finding").FontSize(8).ExtraBold().FontColor(Colors.Indigo.Medium);
                                answerCol.Item().DefaultTextStyle(x => x.LineHeight(1.5f)).Text(t => RenderMarkdown(t, unit.Answer.Content));
                            });

                            unitCol.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(metaCol =>
                            {
                                metaCol.Spacing(4);

                                metaCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(t =>
                                    {
                                        t.Span("Inference: ").ExtraBold().FontSize(8);
                                        t.Span($"Model: {unit.Answer.ModelName ?? _config.Ollama.ChatModel} | Latency: {unit.Answer.ProcessingTimeMs:F0} ms | Tokens: {unit.Answer.TokenCount}").FontSize(8);
                                    });

                                    var avgScore = unit.Answer.Sources.Any() ? unit.Answer.Sources.Average(s => s.Score) : 0;
                                    row.RelativeItem().AlignRight().Text(t =>
                                    {
                                        t.Span("Retrieval: ").ExtraBold().FontSize(8);
                                        t.Span($"Sources: {unit.Answer.Sources.Count} | Avg. Similarity: {avgScore:F4}").FontSize(8);
                                    });
                                });

                                if (unit.Answer.Sources.Any())
                                {
                                    metaCol.Item().PaddingTop(4).Text(t =>
                                    {
                                        t.Span("Evidence Chain: ").ExtraBold().FontSize(8);
                                        var citations = unit.Answer.Sources.Select((s, i) => $"[{i + 1}] {s.SourceDocument} (score {s.Score:F4})");
                                        t.Span(string.Join("; ", citations)).FontSize(8).Italic();
                                    });
                                }
                            });

                            if (unit.Answer.Effectiveness > 0)
                            {
                                var label = unit.Answer.Effectiveness == 1 ? "Positive Validation" : "Negative Validation";
                                var color = unit.Answer.Effectiveness == 1 ? Colors.Green.Medium : Colors.Red.Medium;
                                unitCol.Item().AlignRight().Text(label).FontSize(8).FontColor(color).SemiBold().Italic();
                            }
                        });

                        unitIndex++;
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

    private static List<ResearchUnit> BuildResearchUnits(List<ChatMessageDto> orderedMessages)
    {
        var units = new List<ResearchUnit>();
        ChatMessageDto? pendingQuery = null;

        foreach (var message in orderedMessages)
        {
            if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                pendingQuery = message;
                continue;
            }

            if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                units.Add(new ResearchUnit(pendingQuery, message));
                pendingQuery = null;
            }
        }

        return units;
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

    private sealed record ResearchUnit(ChatMessageDto? Query, ChatMessageDto Answer);
}
