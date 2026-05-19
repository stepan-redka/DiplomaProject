using System.Diagnostics;
using System.Text;
using Diploma.Application.Interfaces.Documents;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Parsers;

public class CodeDocumentParser : IDocumentParser
{
    private readonly ILogger<CodeDocumentParser> _logger;
    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", "C#" },
        { ".cpp", "C++" },
        { ".h", "C/C++ Header" },
        { ".hpp", "C++ Header" },
        { ".c", "C" },
        { ".js", "JavaScript" },
        { ".ts", "TypeScript" },
        { ".py", "Python" },
        { ".java", "Java" },
        { ".go", "Go" },
        { ".rs", "Rust" },
        { ".swift", "Swift" },
        { ".kt", "Kotlin" },
        { ".sql", "SQL" },
        { ".sh", "Shell Script" },
        { ".bat", "Batch Script" },
        { ".ps1", "PowerShell" },
        { ".css", "CSS" },
        { ".scss", "SCSS" }
    };

    public CodeDocumentParser(ILogger<CodeDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        ExtensionToLanguage.ContainsKey(Path.GetExtension(fileName));

    public DocumentType GetDocumentType(string fileName) =>
        DocumentType.SourceCode;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing code document: {FileName}", fileName);
        var sw = Stopwatch.StartNew();
        var extension = Path.GetExtension(fileName);
        var language = ExtensionToLanguage.GetValueOrDefault(extension, "Unknown Code");

        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true);
            var rawContent = await reader.ReadToEndAsync(ct);

            // THESIS NOTE: Adding semantic metadata directly to the parsed stream 
            // improves LLM reasoning over technical assets without requiring complex UI filters.
            var enrichedContent = $"File: {fileName}\nLanguage: {language}\n---\n{rawContent}";

            sw.Stop();
            _logger.LogInformation("Successfully parsed {Language} file {FileName} in {ElapsedMs}ms.",
                language, fileName, sw.ElapsedMilliseconds);

            return new ParsedDocument
            {
                Success = true,
                Content = enrichedContent,
                FileName = fileName,
                Type = DocumentType.SourceCode
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Code parsing canceled for {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to parse code document: {FileName}", fileName);

            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName,
                Type = DocumentType.SourceCode
            };
        }
    }
}
