using Diploma.Infrastructure.Services.Analytics;
using Xunit;
using Moq;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Diploma.Application.Interfaces.Identity;

namespace Diploma.Tests;

public class EvaluationServiceTests
{
    private readonly EvaluationService _service;

    public EvaluationServiceTests()
    {
        var mockUserService = new Mock<ICurrentUserService>();
        // Mock DBContext for simple logic test (NormalizeScore doesn't use DB)
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options, mockUserService.Object);
        _service = new EvaluationService(context);
    }

    [Theory]
    [InlineData(0.95f, 0.5, 1.0)]  // Exact Cap
    [InlineData(0.85f, 0.5, 0.9)]  // Tier 1 boundary
    [InlineData(0.5f, 0.5, 0.6)]   // Threshold boundary
    [InlineData(0.0f, 0.5, 0.0)]   // Zero
    [InlineData(0.90f, 0.5, 0.95)] // Mid Tier 1
    [InlineData(0.675f, 0.5, 0.75)] // Mid Tier 2
    [InlineData(0.25f, 0.5, 0.3)]  // Mid Tier 3
    public void NormalizeScore_ShouldReturnExpectedValues(float raw, double threshold, double expected)
    {
        var result = _service.NormalizeScore(raw, threshold);
        Assert.InRange(result, expected - 0.001, expected + 0.001);
    }
}
