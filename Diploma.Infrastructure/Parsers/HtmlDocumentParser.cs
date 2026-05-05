using System.Diagnostics;
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
        _logger.LogInformation("Parsing HTML document: {FileName}", fileName);
        var sw = Stopwatch.StartNew();
        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true);
            var content = await reader.ReadToEndAsync();

            sw.Stop();
            _logger.LogInformation("Successfully parsed HTML {FileName} in {ElapsedMs}ms. Length: {Length}", 
                fileName, sw.ElapsedMilliseconds, content.Length);

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
            sw.Stop();
            _logger.LogError(ex, "Failed to parse HTML document: {FileName} after {ElapsedMs}ms", fileName, sw.ElapsedMilliseconds);
            
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
