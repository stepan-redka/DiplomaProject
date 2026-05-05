using System.Diagnostics;
using System.Text;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Parsers;

public class TextDocumentParser : IDocumentParser
{
    private readonly ILogger<TextDocumentParser> _logger;

    public TextDocumentParser(ILogger<TextDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        Path.GetExtension(fileName)
            .Equals(".txt", StringComparison.OrdinalIgnoreCase);

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.PlainText;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName)
    {
        _logger.LogInformation("Parsing text document: {FileName}", fileName);
        var sw = Stopwatch.StartNew();
        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true);
            var content = await reader.ReadToEndAsync();

            sw.Stop();
            _logger.LogInformation("Successfully parsed text document {FileName} in {ElapsedMs}ms. Length: {Length}", 
                fileName, sw.ElapsedMilliseconds, content.Length);

            return new ParsedDocument
            {
                Success = true,
                Content = content,
                FileName = fileName,
                Type = DocumentType.PlainText
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to parse text document: {FileName} after {ElapsedMs}ms", fileName, sw.ElapsedMilliseconds);
            
            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName,
                Type = DocumentType.PlainText
            };
        }
    }
}
