namespace Diploma.Application.Interfaces.Analytics;

public interface IEvaluationService
{
    double NormalizeScore(float rawScore, double threshold);
    Task<bool> SetFeedbackAsync(Guid messageId, string userId, int effectiveness, CancellationToken ct = default);
}
