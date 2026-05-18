using Diploma.Application.Interfaces.Documents;
using System.Text.RegularExpressions;

namespace Diploma.Infrastructure.Services.Documents;

public class TextChunkingService : ITextChunkingService
{
    private static readonly string[] Separators = { "\r\n\r\n", "\n\n", "\r\n", "\n", ". ", "? ", "! ", " ", "" };

    public List<string> ChunkText(string text, int maxChunkSize, int overlap)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        if (maxChunkSize <= 0) return new List<string> { text };

        // Ensure overlap is sane
        overlap = Math.Min(overlap, maxChunkSize / 2);

        return RecursiveSplit(text, Separators, maxChunkSize, overlap);
    }

    private List<string> RecursiveSplit(string text, string[] separators, int maxSize, int overlap)
    {
        var finalChunks = new List<string>();

        // If the text is already small enough, just return it
        if (text.Length <= maxSize)
        {
            return new List<string> { text.Trim() };
        }

        // Find the best separator to use
        string? separator = null;
        string[] remainingSeparators = Array.Empty<string>();

        for (int i = 0; i < separators.Length; i++)
        {
            if (text.Contains(separators[i]))
            {
                separator = separators[i];
                remainingSeparators = separators.Skip(i + 1).ToArray();
                break;
            }
        }

        // Split the text by the chosen separator
        var splits = separator != null 
            ? text.Split(new[] { separator }, StringSplitOptions.None)
            : new[] { text };

        var currentChunk = new List<string>();
        int currentLength = 0;

        foreach (var split in splits)
        {
            string piece = split;
            // Re-attach the separator if it's meaningful (like a period or newline)
            if (separator != null && piece != splits.Last())
            {
                piece += separator;
            }

            if (piece.Length > maxSize)
            {
                // If a single piece is too large, recurse with remaining separators
                if (currentChunk.Any())
                {
                    finalChunks.Add(CombineChunks(currentChunk, overlap));
                    currentChunk.Clear();
                    currentLength = 0;
                }

                if (remainingSeparators.Any())
                {
                    finalChunks.AddRange(RecursiveSplit(piece, remainingSeparators, maxSize, overlap));
                }
                else
                {
                    // Absolute fallback: hard split by character
                    finalChunks.AddRange(HardSplit(piece, maxSize, overlap));
                }
            }
            else if (currentLength + piece.Length > maxSize)
            {
                // Current accumulator is full, save it and start new one with overlap
                finalChunks.Add(CombineChunks(currentChunk, overlap));
                
                // Keep some pieces for overlap
                var overlapPieces = GetOverlapPieces(currentChunk, overlap);
                currentChunk = overlapPieces;
                currentChunk.Add(piece);
                currentLength = currentChunk.Sum(p => p.Length);
            }
            else
            {
                currentChunk.Add(piece);
                currentLength += piece.Length;
            }
        }

        if (currentChunk.Any())
        {
            finalChunks.Add(CombineChunks(currentChunk, 0));
        }

        return finalChunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    private string CombineChunks(List<string> pieces, int overlap)
    {
        return string.Join("", pieces).Trim();
    }

    private List<string> GetOverlapPieces(List<string> pieces, int overlap)
    {
        if (overlap <= 0) return new List<string>();
        
        var result = new List<string>();
        int length = 0;
        
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            if (length + pieces[i].Length <= overlap)
            {
                result.Insert(0, pieces[i]);
                length += pieces[i].Length;
            }
            else
            {
                break;
            }
        }
        
        return result;
    }

    private List<string> HardSplit(string text, int maxSize, int overlap)
    {
        var chunks = new List<string>();
        int start = 0;
        while (start < text.Length)
        {
            int length = Math.Min(maxSize, text.Length - start);
            chunks.Add(text.Substring(start, length));
            start += (maxSize - overlap);
            if (maxSize <= overlap) break; // Safety
        }
        return chunks;
    }
}