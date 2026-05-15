using Diploma.Application.Interfaces;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diploma.Infrastructure.Services;

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
        // We want to transform them into a "confidence" percentage that feels natural to users.
        // A score at the threshold should feel like "Fairly Relevant" (~50-60%)
        // A score of 0.8+ should feel like "Highly Relevant" (90%+)
        
        if (rawScore >= 0.9) return rawScore; // Already very high
        
        // Linear mapping from [threshold, 0.9] to [0.6, 0.95]
        double normalized = 0.6 + (rawScore - threshold) * (0.35 / (0.9 - threshold));
        return Math.Clamp(normalized, 0.0, 1.0);
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
