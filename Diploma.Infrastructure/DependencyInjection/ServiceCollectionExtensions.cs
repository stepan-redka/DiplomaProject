using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Analytics;
using Diploma.Application.Interfaces.Chat;
using Diploma.Application.Interfaces.Documents;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.Interfaces.Storage;
using Diploma.Infrastructure.Parsers;
using Diploma.Infrastructure.Persistence;
using Diploma.Infrastructure.Services.AI;
using Diploma.Infrastructure.Services.Analytics;
using Diploma.Infrastructure.Services.Chat;
using Diploma.Infrastructure.Services.Documents;
using Diploma.Infrastructure.Services.Identity;
using Diploma.Infrastructure.Services.Ingestion;
using Diploma.Infrastructure.ML;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using Microsoft.IO;
using Microsoft.SemanticKernel;

namespace Diploma.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Bind RagConfiguration
        var ragConfig = configuration.GetSection("RagConfiguration").Get<RagConfiguration>() ?? new RagConfiguration();
        services.AddSingleton(ragConfig);

        // --- ML.NET INTENT CLASSIFICATION ---
        var modelPath = Path.Combine(contentRootPath, "Data", "intent_model.zip");
        var jsonPath = Path.Combine(contentRootPath, "Data", "clinc150", "data", "data_full.json");

        // Ensure Data directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);

        if (!File.Exists(modelPath) && File.Exists(jsonPath))
        {
            ModelTrainer.TrainAndSaveModel(jsonPath, modelPath);
        }

        services.AddPredictionEnginePool<IntentData, IntentPrediction>()
            .FromFile(modelPath);

        services.AddScoped<IIntentClassifier, IntentMLClassifier>();

        // --- VECTOR DATABASE ---
        services.AddSingleton<Qdrant.Client.QdrantClient>(sp =>
        {
            var config = sp.GetRequiredService<RagConfiguration>();
            return new Qdrant.Client.QdrantClient(config.Qdrant.Host, config.Qdrant.Port);
        });
        services.AddScoped<IVectorDatabase, QdrantVectorDatabase>();

        // --- SEMANTIC KERNEL & AI ---
        services.AddKernel();
        services.AddOllamaChatCompletion(
            modelId: ragConfig.Ollama.ChatModel,
            endpoint: new Uri(ragConfig.Ollama.Endpoint)
        );
        services.AddOllamaEmbeddingGenerator(
            modelId: ragConfig.Ollama.EmbeddingModel,
            endpoint: new Uri(ragConfig.Ollama.Endpoint)
        );
        services.AddScoped<IAiService, AiService>();

        // --- IDENTITY & SECURITY ---
        services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddDataProtection()
            .PersistKeysToDbContext<ApplicationDbContext>();

        // --- DOCUMENT PARSING ---
        services.AddScoped<IDocumentParser, PdfDocumentParser>();
        services.AddScoped<IDocumentParser, DocxDocumentParser>();
        services.AddScoped<IDocumentParser, TextDocumentParser>();
        services.AddScoped<IDocumentParser, MarkdownDocumentParser>();
        services.AddScoped<IDocumentParser, HtmlDocumentParser>();
        services.AddScoped<IDocumentParser, LatexDocumentParser>();
        services.AddScoped<IDocumentParser, CodeDocumentParser>();
        services.AddScoped<IDocumentParser, CsvDocumentParser>();
        services.AddScoped<IDocumentParser, ExcelDocumentParser>();
        services.AddScoped<IDocumentParser, FallbackTextParser>();
        services.AddScoped<IDocumentParsingService, DocumentParsingService>();

        // --- CORE RAG SERVICES ---
        services.AddScoped<IRagService, RagService>();
        services.AddScoped<ISemanticCacheService, SemanticCacheService>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        services.AddScoped<IIntentResolver, IntentResolver>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();
        services.AddSingleton<ITokenizerService, TokenizerService>();
        services.AddSingleton<IPromptRegistry, PromptRegistry>();
        services.AddScoped<IEvaluationService, EvaluationService>();

        // --- DOMAIN SERVICES ---
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChatHistoryService, ChatHistoryService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // --- PERFORMANCE ---
        services.AddSingleton<RecyclableMemoryStreamManager>();
        services.AddSingleton<IngestionChannel>();
        services.AddHostedService<IngestionBackgroundService>();

        return services;
    }
}
