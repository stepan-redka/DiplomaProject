using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces.Documents;

public interface IExportService
{
    byte[] ExportChatHistoryAsJson(IEnumerable<ChatMessageDto> history);
    byte[] ExportSessionAsPdf(ChatSessionDetailDto session, string userEmail);
    byte[] ExportResearchDataAsCsv(IEnumerable<ChatMessageDto> history);
    string GetExportFileName(string extension, string? sessionTitle = null);
}
