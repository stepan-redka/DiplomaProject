using System.Text;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using System.Diagnostics;
using Microsoft.IO;

namespace Diploma.Infrastructure.Parsers;

public class PdfDocumentParser : IDocumentParser
{
    private readonly RecyclableMemoryStreamManager _streamManager;
    private readonly ILogger<PdfDocumentParser> _logger;

    public PdfDocumentParser(RecyclableMemoryStreamManager streamManager, ILogger<PdfDocumentParser> logger)
    {
        _streamManager = streamManager;
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        Path.GetExtension(fileName)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.Pdf;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing PDF document: {FileName}", fileName);
        var sw = Stopwatch.StartNew();
        var textBuilder = new StringBuilder();

        try
        {
            // PERFORMANCE: Use RecyclableMemoryStream to prevent LOH pressure
            using var memoryStream = _streamManager.GetStream();
            await fileStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            if (!await IsRealPdfAsync(memoryStream, ct))
            {
                _logger.LogWarning(
                    "File {FileName} has .pdf extension but missing PDF header. Falling back to text.",
                    fileName);

                return await ParseAsFallbackTextAsync(memoryStream, fileName, ct);
            }

            // PdfPig's Open is synchronous, but we handle cancellation by wrapping the loop
            using var document = PdfDocument.Open(
                memoryStream,
                new ParsingOptions { UseLenientParsing = true });

            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                textBuilder.AppendLine(page.Text);
            }

            sw.Stop();
            var content = textBuilder.ToString().Trim();
            _logger.LogInformation("Successfully parsed PDF {FileName} in {ElapsedMs}ms. Length: {Length} characters, Pages: {PageCount}", 
                fileName, sw.ElapsedMilliseconds, content.Length, document.NumberOfPages);

            return new ParsedDocument
            {
                Success = true,
                Content = content,
                FileName = fileName,
                Type = DocumentType.Pdf
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("PDF parsing canceled for {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to parse PDF: {FileName} after {ElapsedMs}ms", fileName, sw.ElapsedMilliseconds);

            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName
            };
        }
    }

    private static async Task<bool> IsRealPdfAsync(Stream stream, CancellationToken ct)
    {
        var headerBytes = new byte[5];
        if (await stream.ReadAsync(headerBytes, 0, 5, ct) < 5)
        {
            stream.Position = 0;
            return false;
        }

        stream.Position = 0;
        return Encoding.ASCII
            .GetString(headerBytes)
            .StartsWith("%PDF");
    }

    private async Task<ParsedDocument> ParseAsFallbackTextAsync(Stream stream, string fileName, CancellationToken ct)
    {
        stream.Position = 0;
        _logger.LogInformation("Attempting fallback text parsing for {FileName}", fileName);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync(ct);

        return new ParsedDocument
        {
            Success = true,
            Content = content,
            FileName = fileName,
            Type = DocumentType.PlainText
        };
    }
}