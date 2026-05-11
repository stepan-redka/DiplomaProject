using System.Text;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Infrastructure.Services;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
    private readonly Mock<ILogger<IngestionBackgroundService>> _mockLogger;
    private readonly IngestionChannel _channel;
    private readonly ApplicationDbContext _dbContext;
    private readonly CurrentUserService _realUserService;

    public IngestionBackgroundServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockParsingService = new Mock<IDocumentParsingService>();
        _mockRagService = new Mock<IRagService>();
        _mockLogger = new Mock<ILogger<IngestionBackgroundService>>();
        _channel = new IngestionChannel();

        // Setup real user service for the 'as' cast
        var mockHttpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        _realUserService = new CurrentUserService(mockHttpContextAccessor.Object);

        // Setup InMemory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options, _realUserService);

        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
        
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(_mockScopeFactory.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ApplicationDbContext))).Returns(_dbContext);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IDocumentParsingService))).Returns(_mockParsingService.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IRagService))).Returns(_mockRagService.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ICurrentUserService))).Returns(_realUserService);
        
        // Mock GetRequiredService behavior
        _mockServiceProvider.Setup(x => x.GetService(typeof(IEnumerable<ILogger<IngestionBackgroundService>>)))
            .Returns(new List<ILogger<IngestionBackgroundService>>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessTaskFromChannel()
    {
        // Arrange
        var userId = "test-user";
        var fileName = "test.txt";
        var fileData = Encoding.UTF8.GetBytes("test content");
        var task = new IngestionTask(fileData, fileName, userId);

        _mockParsingService.Setup(p => p.ParseDocumentAsync(It.IsAny<Stream>(), fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParsedDocument { Success = true, Content = "Parsed Content" });

        var service = new IngestionBackgroundService(_channel, _mockServiceProvider.Object, _mockLogger.Object);
        
        // Act
        var executeTask = service.StartAsync(CancellationToken.None);
        await _channel.Writer.WriteAsync(task);
        _channel.Writer.Complete();
        
        // Wait for the service to finish processing the channel
        await Task.Delay(1000); 
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _mockRagService.Verify(r => r.IngestDocumentAsync("Parsed Content", fileName, It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify document was created in DB
        var doc = await _dbContext.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.FileName == fileName);
        Assert.NotNull(doc);
        Assert.Equal(userId, doc.UserId);
        Assert.Equal(Diploma.Domain.Enums.IngestionStatus.Success, doc.Status);
    }
}
