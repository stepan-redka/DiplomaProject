using Diploma.Application.Interfaces;
using Diploma.Infrastructure.AI;
using Qdrant.Client;
using Diploma.Infrastructure.Parsers;
using Diploma.Infrastructure.Services;
using Diploma.Infrastructure.Persistence;
using Diploma.Application.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

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

builder.Services.AddControllersWithViews();

// Register Document Parsers
builder.Services.AddScoped<IDocumentParser, PdfDocumentParser>();
builder.Services.AddScoped<IDocumentParser, DocxDocumentParser>();
builder.Services.AddScoped<IDocumentParser, TextDocumentParser>();
builder.Services.AddScoped<IDocumentParser, MarkdownDocumentParser>();
builder.Services.AddScoped<IDocumentParser, HtmlDocumentParser>();
builder.Services.AddScoped<IDocumentParser, LatexDocumentParser>();
builder.Services.AddScoped<IDocumentParser, FallbackTextParser>();

builder.Services.AddScoped<IVectorDatabase, QdrantVectorDatabase>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<ITextChunkingService, TextChunkingService>();
builder.Services.AddScoped<IRagService, RagService>();

// Register Document Parsing Orchestrator
builder.Services.AddScoped<IDocumentParsingService, DocumentParsingService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();