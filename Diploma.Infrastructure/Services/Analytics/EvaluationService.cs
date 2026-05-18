using Diploma.Application.Interfaces.Analytics;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diploma.Infrastructure.Services.Analytics;

public class EvaluationService : IEvaluationService
{
    private readonly ApplicationDbContext _dbContext;

    public EvaluationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public double NormalizeScore(float rawScore, double threshold)
    {
        // Vector similarity scores (Cosine) often cluster. 
        // We transform them into a "confidence" percentage that feels natural to users.
        
        if (rawScore >= 0.95f) return 1.0;
        if (rawScore <= 0.0f) return 0.0;

        // Tier 1: Highly Relevant (0.85 - 0.95) -> 90% - 100%
        if (rawScore >= 0.85f)
        {
            return 0.9 + (rawScore - 0.85) * (0.1 / (0.95 - 0.85));
        }

        // Tier 2: Relevant (threshold - 0.85) -> 60% - 90%
        if (rawScore >= threshold)
        {
            return 0.6 + (rawScore - threshold) * (0.3 / (0.85 - threshold));
        }

        // Tier 3: Low Relevance (0 - threshold) -> 0% - 60%
        return rawScore * (0.6 / threshold);
    }

    public async Task<bool> SetFeedbackAsync(Guid messageId, string userId, int effectiveness, CancellationToken ct = default)
    {
        var message = await _dbContext.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId, ct);

        if (message == null) return false;

        message.Effectiveness = (MessageEffectiveness)effectiveness;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}
