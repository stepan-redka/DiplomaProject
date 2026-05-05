using Diploma.Application.Interfaces;

namespace Diploma.Infrastructure.Services;

public class TextChunkingService : ITextChunkingService
{
    public List<string> ChunkText(string text, int maxChunkSize, int overlap)
    {
        var chunks = new List<string>();
        
        if (string.IsNullOrWhiteSpace(text) || maxChunkSize <= 0)
            return chunks;

        // Ensure overlap is less than the max chunk size to prevent stalls
        if (overlap >= maxChunkSize)
            overlap = maxChunkSize / 2;

        int startIndex = 0;
        while (startIndex < text.Length)
        {
            // 1. Determine initial window
            int length = Math.Min(maxChunkSize, text.Length - startIndex);
            int endIndex = startIndex + length;

            // 2. Try to find a natural break point (space) within the window
            // but only if we haven't reached the end of the text
            if (endIndex < text.Length)
            {
                int lastSpace = text.LastIndexOf(' ', endIndex, length);
                if (lastSpace > startIndex)
                {
                    endIndex = lastSpace;
                }
            }

            // 3. Extract chunk
            string chunk = text.Substring(startIndex, endIndex - startIndex).Trim();
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(chunk);
            }

            // 4. Calculate next starting position
            int nextStartIndex = endIndex - overlap;

            // Safety check: ensure we always progress to avoid infinite loops
            if (nextStartIndex <= startIndex)
            {
                startIndex = endIndex; 
            }
            else
            {
                startIndex = nextStartIndex;
            }
        }

        return chunks;
    }
}