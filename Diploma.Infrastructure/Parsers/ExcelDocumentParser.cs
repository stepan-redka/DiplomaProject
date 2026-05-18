using System.Text;
using Diploma.Application.Interfaces.Documents;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using ExcelDataReader;
using System.Data;

namespace Diploma.Infrastructure.Parsers;

public class ExcelDocumentParser : IDocumentParser
{
    private readonly ILogger<ExcelDocumentParser> _logger;

    public ExcelDocumentParser(ILogger<ExcelDocumentParser> logger)
    {
        _logger = logger;
        // Required for ExcelDataReader on .NET Core
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLower();
        return ext == ".xls" || ext == ".xlsx";
    }

    public DocumentType GetDocumentType(string fileName) => DocumentType.Excel;

    public async Task<ParsedDocument> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing Excel document: {FileName}", fileName);
        var sw = Stopwatch.StartNew();
        var textBuilder = new StringBuilder();

        try
        {
            // ExcelDataReader is synchronous but we can run it in a Task if needed.
            // For now, we'll use it directly as the stream is likely already in memory or local.
            using var reader = ExcelReaderFactory.CreateReader(fileStream);
            var result = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
            });

            textBuilder.AppendLine($"Excel File: {fileName}");
            textBuilder.AppendLine($"Total Sheets: {result.Tables.Count}");
            textBuilder.AppendLine(new string('=', 30));

            foreach (DataTable table in result.Tables)
            {
                ct.ThrowIfCancellationRequested();
                textBuilder.AppendLine($"Sheet: {table.TableName}");
                textBuilder.AppendLine(new string('-', 20));

                foreach (DataRow row in table.Rows)
                {
                    var rowData = new List<string>();
                    foreach (DataColumn column in table.Columns)
                    {
                        rowData.Add($"{column.ColumnName}: {row[column]}");
                    }
                    textBuilder.AppendLine(string.Join(", ", rowData));
                }
                textBuilder.AppendLine();
            }

            sw.Stop();
            var content = textBuilder.ToString().Trim();
            _logger.LogInformation("Successfully parsed Excel {FileName} in {ElapsedMs}ms.", 
                fileName, sw.ElapsedMilliseconds);

            return new ParsedDocument
            {
                Success = true,
                Content = content,
                FileName = fileName,
                Type = DocumentType.Excel
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Excel parsing canceled for {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to parse Excel: {FileName}", fileName);
            return new ParsedDocument
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = fileName
            };
        }
    }
}
