using Diploma.Application.Interfaces;

namespace Diploma.Infrastructure.Services;

public class TokenizerService : ITokenizerService
{
    public int GetTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        // Heuristic: 1 token ~= 4 chars (Standard estimation for GPT-style models)
        return text.Length / 4;
    }
}
