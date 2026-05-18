using Diploma.Domain.Entities;
using Diploma.Domain.Enums;

namespace Diploma.Application.Interfaces.Documents;

/// <summary>
/// Interface for document parsing
/// </summary>
public interface IDocumentParser
{
    Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    DocumentType GetDocumentType(string fileName);
    bool IsSupported(string fileName);
}
