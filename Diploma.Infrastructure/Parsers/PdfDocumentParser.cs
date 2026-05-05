using System.Text;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using System.Diagnostics;

namespace Diploma.Infrastructure.Parsers;

public class PdfDocumentParser : IDocumentParser
{
    private readonly ILogger<PdfDocumentParser> _logger;

    public PdfDocumentParser(ILogger<PdfDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        Path.GetExtension(fileName)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.Pdf;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName)
    {
        _logger.LogInformation("Parsing PDF document: {FileName}", fileName);
        var sw = Stopwatch.StartNew();
        var textBuilder = new StringBuilder();

        try
        {
            // PdfPig requires a seekable stream
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            if (!IsRealPdf(memoryStream))
            {
                _logger.LogWarning(
                    "File {FileName} has .pdf extension but missing PDF header. Falling back to text.",
                    fileName);

                return await ParseAsFallbackText(memoryStream, fileName);
            }

            using var document = PdfDocument.Open(
                memoryStream,
                new ParsingOptions { UseLenientParsing = true });

            foreach (var page in document.GetPages())
            {
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

    private static bool IsRealPdf(Stream stream)
    {
        var headerBytes = new byte[5];

        if (stream.Read(headerBytes, 0, 5) < 5)
        {
            stream.Position = 0;
            return false;
        }

        stream.Position = 0;
        return Encoding.ASCII
            .GetString(headerBytes)
            .StartsWith("%PDF");
    }

    private async Task<ParsedDocument> ParseAsFallbackText(Stream stream, string fileName)
    {
        stream.Position = 0;
        _logger.LogInformation("Attempting fallback text parsing for {FileName}", fileName);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();

        return new ParsedDocument
        {
            Success = true,
            Content = content,
            FileName = fileName,
            Type = DocumentType.PlainText
        };
    }
}