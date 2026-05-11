using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces;

public interface IExportService
{
    byte[] ExportChatHistoryAsJson(IEnumerable<ChatMessageDto> history);
    byte[] ExportChatHistoryAsPdf(IEnumerable<ChatMessageDto> history, string userName);
    string GetExportFileName(string extension);
}
