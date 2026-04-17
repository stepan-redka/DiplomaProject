using System.Text;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Parsers;

public class DocxDocumentParser : IDocumentParser
{
    private readonly ILogger<DocxDocumentParser> _logger;

    public DocxDocumentParser(ILogger<DocxDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        Path.GetExtension(fileName)
            .Equals(".docx", StringComparison.OrdinalIgnoreCase);

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.Docx;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName)
    {
        try
        {
            // Copy to memory stream to ensure seekability, similar to PdfDocumentParser
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            using var wordDocument = WordprocessingDocument.Open(memoryStream, false);
            var body = wordDocument.MainDocumentPart?.Document.Body;
            
            if (body == null)
            {
                return new ParsedDocument
                {
                    Success = true,
                    Content = string.Empty,
                    FileName = fileName,
                    Type = DocumentType.Docx
                };
            }

            // Extract text from paragraphs to preserve basic structure
            var textBuilder = new StringBuilder();
            foreach (var element in body.Elements())
            {
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
