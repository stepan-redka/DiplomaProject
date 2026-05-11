namespace Diploma.Application.DTOs;

/// <summary>
/// Encapsulates a document ingestion task to be processed in the background.
/// </summary>
public record IngestionTask(
    byte[] FileData,
    string FileName,
    string UserId
);
