using System.Text;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Parsers;

public class HtmlDocumentParser : IDocumentParser
{
    private readonly ILogger<HtmlDocumentParser> _logger;

    public HtmlDocumentParser(ILogger<HtmlDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ".html" || ext == ".htm";
    }

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.Html;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName)
    {
        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true);
            var content = await reader.ReadToEndAsync();

            return new ParsedDocument
            {
                Success = true,
                Content = content,
                FileName = fileName,
                Type = DocumentType.Html
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML document: {FileName}", fileName);
            
            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName,
                Type = DocumentType.Html
            };
        }
    }
}
