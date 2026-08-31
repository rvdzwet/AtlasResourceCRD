using System;
using System.IO;
using System.Net.Http;
using Atlas.Core.Gemini;
using Atlas.Server.Agents;
using Atlas.Server.Components;
using Atlas.Server.Graph;
using Atlas.Server.Services;
using Atlas.Server.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Blazor Server with Interactive Server Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 2. Add Embedded REST API Controllers & Caching
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// 3. Register Core Singletons & Graph Infrastructure
builder.Services.AddSingleton<FleetIndexService>();
builder.Services.AddSingleton<ServerCacheRepository>();
builder.Services.AddSingleton<SpecDocumentRepository>();
builder.Services.AddSingleton<Neo4jGraphService>();
builder.Services.AddSingleton<Neo4jGraphMapper>();
builder.Services.AddSingleton<CrossServiceLinkResolver>();
builder.Services.AddSingleton<VendorRfpExportService>();
builder.Services.AddSingleton<Atlas.Core.Security.OsvVulnerabilityClient>();
builder.Services.AddSingleton<Atlas.Core.Security.DepsDevClient>();
builder.Services.AddSingleton<Atlas.Core.Scanner.CycloneDxGenerator>();
builder.Services.AddSingleton<VulnerabilityBackgroundSyncService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<VulnerabilityBackgroundSyncService>());

// 4. Register Multi-Model LLM Client & Agentic Stitching Infrastructure
builder.Services.AddSingleton<ILlmClient>(sp =>
{
    var config = builder.Configuration;
    var llmSettings = config.GetSection("LLM").Get<LlmSettings>() ?? new LlmSettings();
    var activeProfile = llmSettings.GetActiveProfile();

    // Fallback for API key from environment variables
    if (string.IsNullOrWhiteSpace(activeProfile.ApiKey))
    {
        activeProfile.ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                               ?? Environment.GetEnvironmentVariable("ATLAS_API_KEY")
                               ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    }

    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return LlmClientFactory.Create(activeProfile, loggerFactory);
});

builder.Services.AddSingleton<ArchitectureStitchingAgent>();
builder.Services.AddHostedService<StitchingBackgroundQueueService>();

var app = builder.Build();

// 5. Configure Middleware Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// 6. Map Controllers & Blazor Components
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Logger.LogInformation("================================================================================");
app.Logger.LogInformation("Atlas Server Hub v1.7.0 started on {Urls}", string.Join(", ", app.Urls));
app.Logger.LogInformation("Theme: Stater Enterprise Design System (#562178 Royal Purple / #F8A719 Gold)");
app.Logger.LogInformation("================================================================================");

app.Run();
