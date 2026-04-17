using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Parsers;

public class DocumentParsingService : IDocumentParsingService
{
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly ILogger<DocumentParsingService> _logger;

    public DocumentParsingService(IEnumerable<IDocumentParser> parsers, ILogger<DocumentParsingService> logger)
    {
        _parsers = parsers;
        _logger = logger;
    }

    public async Task<ParsedDocument> ParseDocumentAsync(Stream fileStream, string fileName)
    {
        // 1. Find a specialized parser (exclude the fallback one from initial check)
        var specializedParser = _parsers
            .Where(p => p is not FallbackTextParser)
            .FirstOrDefault(p => p.IsSupported(fileName));

        if (specializedParser != null)
        {
            _logger.LogInformation("Using specialized parser {ParserType} for file {FileName}", specializedParser.GetType().Name, fileName);
            return await specializedParser.ParseAsync(fileStream, fileName);
        }

        // 2. Fallback to the safety parser
        var fallbackParser = _parsers.OfType<FallbackTextParser>().FirstOrDefault();
        if (fallbackParser != null)
        {
            return await fallbackParser.ParseAsync(fileStream, fileName);
        }

        // 3. Absolute failure (should not happen if FallbackTextParser is registered)
        return new ParsedDocument
        {
            Success = false,
            ErrorMessage = $"No parser found for file: {fileName}",
            FileName = fileName
        };
    }
}
