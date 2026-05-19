using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Diploma.Application.Interfaces.Documents;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Diploma.Infrastructure.Parsers;

public class CsvDocumentParser : IDocumentParser
{
    private readonly ILogger<CsvDocumentParser> _logger;

    public CsvDocumentParser(ILogger<CsvDocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName) =>
        Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);

    public DocumentType GetDocumentType(string fileName) => DocumentType.Csv;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing CSV document: {FileName}", fileName);
        var sw = Stopwatch.StartNew();
        var textBuilder = new StringBuilder();

        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null,
                BadDataFound = null
            };

            using var csv = new CsvReader(reader, config);

            if (await csv.ReadAsync())
            {
                csv.ReadHeader();
                var headers = csv.HeaderRecord;

                if (headers != null)
                {
                    textBuilder.AppendLine($"CSV File: {fileName}");
                    textBuilder.AppendLine("Headers: " + string.Join(" | ", headers));
                    textBuilder.AppendLine(new string('-', 20));

                    int rowCount = 0;
                    while (await csv.ReadAsync())
                    {
                        ct.ThrowIfCancellationRequested();
                        var row = new List<string>();
                        foreach (var header in headers)
                        {
                            row.Add($"{header}: {csv.GetField(header)}");
                        }
                        textBuilder.AppendLine($"Row {++rowCount}: " + string.Join(", ", row));
                    }

                    sw.Stop();
                    var content = textBuilder.ToString().Trim();
                    _logger.LogInformation("Successfully parsed CSV {FileName} in {ElapsedMs}ms. Rows: {RowCount}",
                        fileName, sw.ElapsedMilliseconds, rowCount);

                    return new ParsedDocument
                    {
                        Success = true,
                        Content = content,
                        FileName = fileName,
                        Type = DocumentType.Csv
                    };
                }
            }

            return new ParsedDocument
            {
                Success = true,
                Content = "Empty CSV file.",
                FileName = fileName,
                Type = DocumentType.Csv
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CSV parsing canceled for {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to parse CSV: {FileName}", fileName);
            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName
            };
        }
    }
}
