using Diploma.Application.Interfaces.Documents;
using Diploma.Domain.Entities;
using Microsoft.Extensions.Logging;
using Diploma.Infrastructure.Parsers;

namespace Diploma.Infrastructure.Services.Documents;

public class DocumentParsingService : IDocumentParsingService
{
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly ILogger<DocumentParsingService> _logger;

    public DocumentParsingService(IEnumerable<IDocumentParser> parsers, ILogger<DocumentParsingService> logger)
    {
        _parsers = parsers;
        _logger = logger;
    }

    public async Task<ParsedDocument> ParseDocumentAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var specializedParser = _parsers
            .Where(p => p is not FallbackTextParser)
            .FirstOrDefault(p => p.IsSupported(fileName));

        if (specializedParser != null)
        {
            _logger.LogInformation("Using specialized parser {ParserType} for file {FileName}", specializedParser.GetType().Name, fileName);
            return await specializedParser.ParseAsync(fileStream, fileName, ct);
        }

        var fallbackParser = _parsers.OfType<FallbackTextParser>().FirstOrDefault();
        if (fallbackParser != null)
        {
            return await fallbackParser.ParseAsync(fileStream, fileName, ct);
        }

        return new ParsedDocument
        {
            Success = false,
            ErrorMessage = $"No parser found for file: {fileName}",
            FileName = fileName
        };
    }
}
