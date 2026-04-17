using Diploma.Application.Interfaces;
using Diploma.Infrastructure.Parsers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register Document Parsers
builder.Services.AddScoped<IDocumentParser, PdfDocumentParser>();
builder.Services.AddScoped<IDocumentParser, DocxDocumentParser>();
builder.Services.AddScoped<IDocumentParser, TextDocumentParser>();
builder.Services.AddScoped<IDocumentParser, MarkdownDocumentParser>();
builder.Services.AddScoped<IDocumentParser, HtmlDocumentParser>();
builder.Services.AddScoped<IDocumentParser, LatexDocumentParser>();
builder.Services.AddScoped<IDocumentParser, FallbackTextParser>();

// Register Document Parsing Orchestrator
builder.Services.AddScoped<IDocumentParsingService, DocumentParsingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();