using Diploma.Domain.Enums;

namespace Diploma.Infrastructure.Utils;

public static class StorageFormatter
{
    public static string FormatSize(long bytes)
    {
        var units = Enum.GetNames<StorageUnit>();
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:F1} {units[unitIndex]}";
    }
}
