using System.Text;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Parsers;

public class MarkdownDocumentParser : IDocumentParser
{
    private readonly ILogger<MarkdownDocumentParser> _logger;

    public MarkdownDocumentParser(ILogger<MarkdownDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        Path.GetExtension(fileName)
            .Equals(".md", StringComparison.OrdinalIgnoreCase);

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.Markdown;

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
                Type = DocumentType.Markdown
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Markdown document: {FileName}", fileName);
            
            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName,
                Type = DocumentType.Markdown
            };
        }
    }
}
