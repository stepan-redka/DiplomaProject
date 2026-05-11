using System.Text;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diploma.Tests;

public class IngestionBackgroundServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IDocumentParsingService> _mockParsingService;
    private readonly Mock<IRagService> _mockRagService;
    private readonly Mock<ICurrentUserService> _mockUserService;
    private readonly Mock<ILogger<IngestionBackgroundService>> _mockLogger;
    private readonly IngestionChannel _channel;

    public IngestionBackgroundServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockParsingService = new Mock<IDocumentParsingService>();
        _mockRagService = new Mock<IRagService>();
        
        // We need a mock that can be cast to CurrentUserService or just use the real one if possible
        // But the service uses 'as CurrentUserService' which is a bit tight.
        // Let's use a mock of HttpContextAccessor to create a real CurrentUserService
        var mockHttpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        _mockUserService = new Mock<ICurrentUserService>();

        _mockLogger = new Mock<ILogger<IngestionBackgroundService>>();
        _channel = new IngestionChannel();

        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
        
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(_mockScopeFactory.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IDocumentParsingService))).Returns(_mockParsingService.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IRagService))).Returns(_mockRagService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessTaskFromChannel()
    {
        // Arrange
        var userId = "test-user";
        var fileName = "test.txt";
        var fileData = Encoding.UTF8.GetBytes("test content");
        var task = new IngestionTask(fileData, fileName, userId);

        // We need the real CurrentUserService because of the 'as' cast in the service
        var realUserService = new CurrentUserService(new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ICurrentUserService))).Returns(realUserService);

        _mockParsingService.Setup(p => p.ParseDocumentAsync(It.IsAny<Stream>(), fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedDocument { Success = true, Content = "Parsed Content" });

        var service = new IngestionBackgroundService(_channel, _mockServiceProvider.Object, _mockLogger.Object);
        
        using var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        // Act
        await _channel.Writer.WriteAsync(task);
        
        // Give it a moment to process
        await Task.Delay(200);
        cts.Cancel();

        // Assert
        _mockRagService.Verify(r => r.IngestDocumentAsync("Parsed Content", fileName, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(userId, realUserService.UserId);
    }
}
