using System.Diagnostics;
using System.Text;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Parsers;

/// <summary>
/// A safety parser that attempts to read any file as UTF-8 text.
/// This is used when no specialized parser matches the file extension.
/// </summary>
public class FallbackTextParser : IDocumentParser
{
    private readonly ILogger<FallbackTextParser> _logger;

    public FallbackTextParser(ILogger<FallbackTextParser> logger)
    {
        _logger = logger;
    }

    // This parser is a catch-all, but we'll return true for everything
    // and rely on the Orchestrator to use it as the last resort.
    public bool IsSupported(string fileName) => true;

    public DocumentType GetDocumentType(string fileName) => DocumentType.Unknown;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName)
    {
        _logger.LogWarning("Using fallback text parser for file: {FileName}. Content might be malformed if it's a binary format.", fileName);
        var sw = Stopwatch.StartNew();
        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true);
            var content = await reader.ReadToEndAsync();

            sw.Stop();
            _logger.LogInformation("Successfully parsed file {FileName} using fallback in {ElapsedMs}ms. Length: {Length}", 
                fileName, sw.ElapsedMilliseconds, content.Length);

            return new ParsedDocument
            {
                Success = true,
                Content = content,
                FileName = fileName,
                Type = DocumentType.Unknown
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Fallback parser failed for file: {FileName} after {ElapsedMs}ms", fileName, sw.ElapsedMilliseconds);
            
            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = $"Fallback parsing failed: {ex.Message}",
                FileName = fileName,
                Type = DocumentType.Unknown
            };
        }
    }
}
