using System.Text;
using Diploma.Application.Interfaces.Documents;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

namespace Diploma.Infrastructure.Parsers;

public class DocxDocumentParser : IDocumentParser
{
    private readonly RecyclableMemoryStreamManager _streamManager;
    private readonly ILogger<DocxDocumentParser> _logger;

    public DocxDocumentParser(RecyclableMemoryStreamManager streamManager, ILogger<DocxDocumentParser> logger)
    {
        _streamManager = streamManager;
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        Path.GetExtension(fileName)
            .Equals(".docx", StringComparison.OrdinalIgnoreCase);

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.Docx;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        try
        {
            // PERFORMANCE: Use RecyclableMemoryStream to ensure seekability without LOH pressure
            using var memoryStream = _streamManager.GetStream();
            await fileStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;
            _logger.LogInformation("Parsing DOCX document: {FileName}. Size: {FileSize} bytes", fileName, memoryStream.Length);

            // OpenXml is synchronous, but we can check cancellation in loops
            using var wordDocument = WordprocessingDocument.Open(memoryStream, false);
            var body = wordDocument.MainDocumentPart?.Document?.Body;

            if (body == null)
            {
                _logger.LogWarning("DOCX document has no body: {FileName}", fileName);
                return new ParsedDocument
                {
                    Success = true,
                    Content = string.Empty,
                    FileName = fileName,
                    Type = DocumentType.Docx
                };
            }

            var textBuilder = new StringBuilder();
            foreach (var element in body.Elements())
            {
                ct.ThrowIfCancellationRequested();
                var text = element.InnerText;
                if (!string.IsNullOrEmpty(text))
                {
                    textBuilder.AppendLine(text);
                }
            }

            return new ParsedDocument
            {
                Success = true,
                Content = textBuilder.ToString().Trim(),
                FileName = fileName,
                Type = DocumentType.Docx
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("DOCX parsing canceled for {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse DOCX: {FileName}", fileName);

            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName,
                Type = DocumentType.Docx
            };
        }
    }
}
