using Diploma.Application.Interfaces;
using Qdrant.Client;
using Diploma.Infrastructure.Parsers;
using Diploma.Infrastructure.Services;
using Diploma.Infrastructure.Persistence;
using Diploma.Application.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.IO;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Bind RagConfiguration
var ragConfig = builder.Configuration.GetSection("RagConfiguration").Get<RagConfiguration>() ?? new RagConfiguration();
builder.Services.AddSingleton(ragConfig);

builder.Services.AddSingleton<Qdrant.Client.QdrantClient>(sp =>
{
    var config = sp.GetRequiredService<RagConfiguration>();
    return new Qdrant.Client.QdrantClient(config.Qdrant.Host, config.Qdrant.Port);
});

// Semantic Kernel with Ollama
builder.Services.AddKernel();
builder.Services.AddOllamaChatCompletion(
    modelId: ragConfig.Ollama.ChatModel,
    endpoint: new Uri(ragConfig.Ollama.Endpoint)
);
builder.Services.AddOllamaEmbeddingGenerator(
    modelId: ragConfig.Ollama.EmbeddingModel,
    endpoint: new Uri(ragConfig.Ollama.Endpoint)
);


builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// --- DATA PROTECTION PERSISTENCE ---
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Enable AJAX support for Antiforgery
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

// Register Document Parsers
builder.Services.AddScoped<IDocumentParser, PdfDocumentParser>();
builder.Services.AddScoped<IDocumentParser, DocxDocumentParser>();
builder.Services.AddScoped<IDocumentParser, TextDocumentParser>();
builder.Services.AddScoped<IDocumentParser, MarkdownDocumentParser>();
builder.Services.AddScoped<IDocumentParser, HtmlDocumentParser>();
builder.Services.AddScoped<IDocumentParser, LatexDocumentParser>();
builder.Services.AddScoped<IDocumentParser, CodeDocumentParser>();
builder.Services.AddScoped<IDocumentParser, CsvDocumentParser>();
builder.Services.AddScoped<IDocumentParser, ExcelDocumentParser>();
builder.Services.AddScoped<IDocumentParser, FallbackTextParser>();

builder.Services.AddScoped<IVectorDatabase, QdrantVectorDatabase>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<ITextChunkingService, TextChunkingService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IChatHistoryService, ChatHistoryService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IHealthService, HealthService>();

// Register Specialized RAG Services
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IIntentResolver, IntentResolver>();
builder.Services.AddSingleton<IPromptRegistry, PromptRegistry>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddSingleton<ITokenizerService, TokenizerService>();
builder.Services.AddScoped<IRetrievalService, RetrievalService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

// Register Document Parsing Orchestrator
builder.Services.AddScoped<IDocumentParsingService, DocumentParsingService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// --- PERFORMANCE & BACKGROUND PROCESSING ---
builder.Services.AddSingleton<RecyclableMemoryStreamManager>();
builder.Services.AddSingleton<IngestionChannel>();
builder.Services.AddHostedService<IngestionBackgroundService>();

var app = builder.Build();

// --- DATABASE MIGRATIONS ON STARTUP ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        if (context.Database.IsRelational())
        {
            context.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. 
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
