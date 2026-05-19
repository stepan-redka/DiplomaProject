using Diploma.Application.Interfaces.Analytics;

namespace Diploma.Infrastructure.Services.Analytics;

public class TokenizerService : ITokenizerService
{
    public int GetTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        // Heuristic: 1 token ~= 4 chars (Standard estimation for GPT-style models)
        return text.Length / 4;
    }
}
