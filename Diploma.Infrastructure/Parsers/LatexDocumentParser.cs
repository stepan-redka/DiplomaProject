using System.Diagnostics;
using System.Text;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Parsers;

public class LatexDocumentParser : IDocumentParser
{
    private readonly ILogger<LatexDocumentParser> _logger;

    public LatexDocumentParser(ILogger<LatexDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        Path.GetExtension(fileName)
            .Equals(".tex", StringComparison.OrdinalIgnoreCase);

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.LaTeX;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName)
    {
        _logger.LogInformation("Parsing LaTeX document: {FileName}", fileName);
        var sw = Stopwatch.StartNew();
        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true);
            var content = await reader.ReadToEndAsync();

            sw.Stop();
            _logger.LogInformation("Successfully parsed LaTeX {FileName} in {ElapsedMs}ms. Length: {Length}", 
                fileName, sw.ElapsedMilliseconds, content.Length);

            return new ParsedDocument
            {
                Success = true,
                Content = content,
                FileName = fileName,
                Type = DocumentType.LaTeX
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to parse LaTeX document: {FileName} after {ElapsedMs}ms", fileName, sw.ElapsedMilliseconds);
            
            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName,
                Type = DocumentType.LaTeX
            };
        }
    }
}
