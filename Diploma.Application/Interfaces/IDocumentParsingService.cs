using Diploma.Domain.Entities;

namespace Diploma.Application.Interfaces;

/// <summary>
/// Orchestrator service for document parsing
/// </summary>
public interface IDocumentParsingService
{
    Task<ParsedDocument> ParseDocumentAsync(Stream fileStream, string fileName);
}
