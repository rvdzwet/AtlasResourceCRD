using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Atlas.Core.Agents;
using Atlas.Core.Gemini;
using Atlas.Core.Html;
using Atlas.Core.Models;
using Atlas.Core.Scanner;
using Atlas.Core.Schema;
using Atlas.Core.Serialization;
using Atlas.Core.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Scanner.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("-h") || args.Contains("--help") || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var cmdArgs = args.Skip(1).ToArray();

        var logLevel = LogLevel.Information;
        if (args.Contains("-vv") || args.Contains("--trace"))
        {
            logLevel = LogLevel.Trace;
        }
        else if (args.Contains("-v") || args.Contains("--verbose"))
        {
            logLevel = LogLevel.Debug;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(logLevel);
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
        });

        var logger = loggerFactory.CreateLogger("Atlas.Scanner.Cli");

        try
        {
            return command switch
            {
                "scan" => await RunScanCommand(cmdArgs, loggerFactory, logger),
                "sbom" => await RunSbomCommand(cmdArgs, logger),
                "delete" => await RunDeleteCommand(cmdArgs, logger),
                "validate" => RunValidateCommand(cmdArgs, logger),
                "html" or "render" => RunHtmlCommand(cmdArgs, logger),
                "schema" => RunSchemaCommand(cmdArgs, logger),
                "init" => RunInitCommand(cmdArgs, logger),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error executing command '{Command}'", command);
            return 1;
        }
    }

    private static async Task<int> RunScanCommand(string[] args, ILoggerFactory loggerFactory, ILogger logger)
    {
        var targetPath = ".";
        string? outputFile = null;
        string? serviceName = null;
        string? profileName = null;
        string? provider = null;
        string? model = null;
        string? endpoint = null;
        string? apiKey = null;
        string? thinkingLevel = null;
        int? contextWindow = null;
        var disableCache = false;
        var clearCache = false;
        var forceSynth = false;
        var noOpen = false;
        var concurrency = 16;
        var maxFiles = int.MaxValue;
        string serverUrl = Environment.GetEnvironmentVariable("ATLAS_SERVER_URL") ?? "http://localhost:5000";

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-n" or "--name" or "--service-name" && i + 1 < args.Length)
            {
                serviceName = args[++i];
            }
            else if (arg is "-p" or "--profile" && i + 1 < args.Length)
            {
                profileName = args[++i];
            }
            else if (arg is "--provider" && i + 1 < args.Length)
            {
                provider = args[++i];
            }
            else if (arg is "-o" or "--output" && i + 1 < args.Length)
            {
                outputFile = args[++i];
            }
            else if (arg is "-s" or "--server" or "--server-url" && i + 1 < args.Length)
            {
                serverUrl = args[++i];
            }
            else if (arg is "-m" or "--model" && i + 1 < args.Length)
            {
                model = args[++i];
            }
            else if (arg is "-k" or "--api-key" && i + 1 < args.Length)
            {
                apiKey = args[++i];
            }
            else if (arg is "-e" or "--endpoint" && i + 1 < args.Length)
            {
                endpoint = args[++i];
            }
            else if (arg is "--context-window" or "--max-context-tokens" && i + 1 < args.Length && int.TryParse(args[++i], out var cw))
            {
                contextWindow = cw;
            }
            else if (arg is "--thinking" or "--thinking-budget" && i + 1 < args.Length)
            {
                thinkingLevel = args[++i];
            }
            else if (arg is "--concurrency" && i + 1 < args.Length && int.TryParse(args[++i], out var c))
            {
                concurrency = c;
            }
            else if (arg is "--max-files" && i + 1 < args.Length && int.TryParse(args[++i], out var mf))
            {
                maxFiles = mf;
            }
            else if (arg is "--all-files" or "--unlimited")
            {
                maxFiles = int.MaxValue;
            }
            else if (arg is "--no-cache")
            {
                disableCache = true;
            }
            else if (arg is "--clear-cache")
            {
                clearCache = true;
            }
            else if (arg is "--force-synth" or "--force-reduce")
            {
                forceSynth = true;
            }
            else if (arg is "--no-open")
            {
                noOpen = true;
            }
            else if (!arg.StartsWith("-"))
            {
                targetPath = arg;
            }
        }

        // Load Named Profile Settings
        var llmSettings = TryLoadLlmSettingsFromAppSettings();
        var profileConfig = llmSettings.GetActiveProfile(profileName);

        // Apply CLI flag overrides to profile
        if (!string.IsNullOrWhiteSpace(provider)) profileConfig.Provider = provider;
        if (!string.IsNullOrWhiteSpace(model)) profileConfig.Model = model;
        if (!string.IsNullOrWhiteSpace(endpoint)) profileConfig.BaseUrl = endpoint;
        if (!string.IsNullOrWhiteSpace(apiKey)) profileConfig.ApiKey = apiKey;
        if (contextWindow.HasValue && contextWindow.Value > 0) profileConfig.ContextWindow = contextWindow.Value;
        if (!string.IsNullOrWhiteSpace(thinkingLevel)) profileConfig.ThinkingLevel = thinkingLevel;

        // Fallback for API key from environment if using Gemini
        if (string.IsNullOrWhiteSpace(profileConfig.ApiKey) && profileConfig.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            profileConfig.ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                                   ?? Environment.GetEnvironmentVariable("ATLAS_API_KEY")
                                   ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
                                   ?? TryFindApiKeyFromAppSettings();
        }

        if (profileConfig.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(profileConfig.ApiKey))
        {
            logger.LogError("Gemini API key is required when using Gemini provider. Provide it via --api-key <KEY> or GEMINI_API_KEY environment variable (or switch to a local model via --profile local-qwen or --provider openai-compatible).");
            return 1;
        }

        var fullPath = Path.GetFullPath(targetPath);

        logger.LogInformation("================================================================================");
        logger.LogInformation("Atlas Scanner CLI v1.8.0 (Target: {Path})", fullPath);
        logger.LogInformation("LLM Engine: [{Provider}] Model: {Model} | ContextWindow: {ContextWindow} tokens",
            profileConfig.Provider, profileConfig.Model, profileConfig.ContextWindow);
        logger.LogInformation("Atlas Server: {ServerUrl}", serverUrl);
        logger.LogInformation("================================================================================");

        // Check Atlas Server Connectivity
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var serverClient = new Atlas.Core.Client.AtlasServerClient(httpClient, loggerFactory.CreateLogger<Atlas.Core.Client.AtlasServerClient>());
        var isServerHealthy = await serverClient.CheckHealthAsync(serverUrl);
        if (!isServerHealthy)
        {
            logger.LogWarning("Atlas Server at {ServerUrl} is currently offline or unreachable. Remote caching & ingestion will be deferred.", serverUrl);
        }
        else
        {
            logger.LogInformation("Connected to Atlas Server at {ServerUrl} (Remote Cache & Neo4j Ingestion ACTIVE)", serverUrl);
        }

        // 1. Pass 1: Local File Scanning
        var gitExtractor = new GitMetadataExtractor(loggerFactory.CreateLogger<GitMetadataExtractor>());
        var manifestAnalyzer = new ManifestAnalyzer(loggerFactory.CreateLogger<ManifestAnalyzer>());
        var scanner = new RepoScanner(loggerFactory.CreateLogger<RepoScanner>(), manifestAnalyzer, gitExtractor);
        var skeleton = scanner.Scan(fullPath, maxFilesOverride: maxFiles);
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            skeleton.RepoName = serviceName;
            logger.LogInformation("🏷️  Overriding Service Name: '{ServiceName}'", serviceName);
        }

        // Instantiate LLM client via multi-model factory
        var llmClient = LlmClientFactory.Create(profileConfig, loggerFactory, httpClient);
        var summaryAgent = new FileSummaryAgent(llmClient, loggerFactory.CreateLogger<FileSummaryAgent>());
        var pipeline = new AtlasAgentPipeline(llmClient, summaryAgent, loggerFactory.CreateLogger<AtlasAgentPipeline>(), loggerFactory);

        var pipelineOptions = new AtlasAgentPipelineOptions
        {
            Concurrency = concurrency,
            DisableCache = disableCache,
            ForceSynth = forceSynth,
            ServerUrl = isServerHealthy ? serverUrl : null,
            ServerClient = isServerHealthy ? serverClient : null
        };

        var crd = await pipeline.ExecuteAsync(skeleton, pipelineOptions);

        // 3. Validation
        var validation = CrdValidator.Validate(crd);
        if (!validation.IsValid)
        {
            logger.LogError("CRD Validation failed with {Count} errors:", validation.Errors.Count);
            foreach (var err in validation.Errors) logger.LogError("  - {Error}", err);
            return 1;
        }

        foreach (var warning in validation.Warnings)
        {
            logger.LogWarning("Validation notice: {Warning}", warning);
        }

        // 4. Output Generation: Strict 2-Step Sequential Pipeline
        // Step 1: Serialize and write atlas-catalog-item.yaml to disk
        var yamlOutput = CrdYamlSerializer.SerializeYaml(crd);
        var defaultYamlPath = string.IsNullOrWhiteSpace(outputFile) ? Path.Combine(Directory.GetCurrentDirectory(), "atlas-catalog-item.yaml") : Path.GetFullPath(outputFile);
        var htmlOutputPath = Path.ChangeExtension(defaultYamlPath, ".html");

        File.WriteAllText(defaultYamlPath, yamlOutput);
        logger.LogInformation("[Step 1/2] Catalog manifest persisted to disk: {YamlPath}", defaultYamlPath);

        // Step 2: Sequentially read the written manifest from disk to render atlas.html
        var persistedYaml = File.ReadAllText(defaultYamlPath);
        var persistedCrd = CrdYamlSerializer.DeserializeYaml(persistedYaml);
        HtmlVisualizerGenerator.GenerateToFile(persistedCrd, htmlOutputPath);
        logger.LogInformation("[Step 2/2] Interactive HTML dashboard rendered sequentially from {YamlPath}: {HtmlPath}", defaultYamlPath, htmlOutputPath);

        logger.LogInformation("================================================================================");
        logger.LogInformation("Atlas scan complete! [Component: {Name}, Tier: {Tier}]",
            crd.Metadata.Name, crd.Spec.ComponentOverview.Tier);
        logger.LogInformation("================================================================================");

        // 5. Automatically open HTML in default browser
        if (!noOpen)
        {
            try
            {
                logger.LogInformation("Opening interactive documentation in browser...");
                var ps = new ProcessStartInfo
                {
                    FileName = htmlOutputPath,
                    UseShellExecute = true
                };
                Process.Start(ps);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not automatically launch browser: {Message}. Open file manually at {HtmlPath}", ex.Message, htmlOutputPath);
            }
        }

        return 0;
    }

    private static async Task<int> RunSbomCommand(string[] args, ILogger logger)
    {
        var targetPath = args.FirstOrDefault(a => !a.StartsWith("-")) ?? ".";
        string? outputFile = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "-o" or "--output" && i + 1 < args.Length)
            {
                outputFile = args[++i];
            }
        }

        outputFile ??= Path.Combine(Directory.GetCurrentDirectory(), "cyclonedx-bom.json");
        var fullPath = Path.GetFullPath(targetPath);

        logger.LogInformation("Generating CycloneDX 1.5 SBOM for: {Path}", fullPath);

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var gitExtractor = new GitMetadataExtractor(loggerFactory.CreateLogger<GitMetadataExtractor>());
        var manifestAnalyzer = new ManifestAnalyzer(loggerFactory.CreateLogger<ManifestAnalyzer>());
        var scanner = new RepoScanner(loggerFactory.CreateLogger<RepoScanner>(), manifestAnalyzer, gitExtractor);
        var skeleton = scanner.Scan(fullPath);
        var generator = new CycloneDxGenerator();

        var (sbom, _) = await generator.GenerateAsync(skeleton);
        var json = System.Text.Json.JsonSerializer.Serialize(sbom, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(outputFile, json);
        logger.LogInformation("CycloneDX 1.5 SBOM generated with {ComponentCount} components: {OutputPath}",
            sbom.Components?.Count ?? 0, outputFile);

        return 0;
    }

    private static async Task<int> RunDeleteCommand(string[] args, ILogger logger)
    {
        if (args.Length == 0 || args[0].StartsWith("-"))
        {
            logger.LogError("Service name required. Usage: atlas delete <service-name> [--server <url>] [--force]");
            return 1;
        }

        var serviceName = args[0];
        string serverUrl = Environment.GetEnvironmentVariable("ATLAS_SERVER_URL") ?? "http://localhost:5000";
        var force = false;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] is "-s" or "--server" or "--server-url" && i + 1 < args.Length)
            {
                serverUrl = args[++i];
            }
            else if (args[i] is "-f" or "--force")
            {
                force = true;
            }
        }

        if (!force)
        {
            Console.Write($"Are you sure you want to permanently delete '{serviceName}' from Atlas catalog, Neo4j, and cache? [y/N]: ");
            var response = Console.ReadLine();
            if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Deletion canceled.");
                return 0;
            }
        }

        logger.LogInformation("Deleting service '{Service}' from Atlas Server at {ServerUrl}...", serviceName, serverUrl);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        try
        {
            var res = await http.DeleteAsync($"{serverUrl.TrimEnd('/')}/api/v1/catalog/resources/{Uri.EscapeDataString(serviceName)}");
            var body = await res.Content.ReadAsStringAsync();
            if (res.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully deleted '{Service}' from Atlas catalog and Neo4j graph.", serviceName);
                return 0;
            }
            else
            {
                logger.LogError("Failed to delete '{Service}'. HTTP {Status}: {Body}", serviceName, (int)res.StatusCode, body);
                return 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Connection error deleting '{Service}' from {ServerUrl}", serviceName, serverUrl);
            return 1;
        }
    }

    private static int RunValidateCommand(string[] args, ILogger logger)
    {
        var filePath = args.FirstOrDefault(a => !a.StartsWith("-"));
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            logger.LogError("File not found. Specify a valid path: atlas validate <path-to-atlas-catalog-item.yaml>");
            return 1;
        }

        var content = File.ReadAllText(filePath);
        AtlasResource? crd;

        try
        {
            crd = filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? CrdYamlSerializer.DeserializeJson(content)
                : CrdYamlSerializer.DeserializeYaml(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse CRD manifest.");
            return 1;
        }

        if (crd == null)
        {
            logger.LogError("Could not parse file into a valid AtlasResource structure.");
            return 1;
        }

        var result = CrdValidator.Validate(crd);
        if (result.IsValid)
        {
            logger.LogInformation("Validation SUCCESSFUL: '{Name}' (Tier: {Tier}) complies 100% with Atlas CRD Schema.",
                crd.Metadata?.Name ?? "Unknown", crd.Spec?.ComponentOverview?.Tier ?? "Unknown");
            return 0;
        }

        logger.LogError("Validation FAILED with {Count} errors:", result.Errors.Count);
        foreach (var err in result.Errors) logger.LogError("  - {Error}", err);
        return 1;
    }

    private static int RunHtmlCommand(string[] args, ILogger logger)
    {
        var inputPath = args.FirstOrDefault(a => !a.StartsWith("-")) ?? "atlas-catalog-item.yaml";
        string? outputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "-o" or "--output" && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
        }

        if (!File.Exists(inputPath))
        {
            logger.LogError("Input manifest not found at: {Path}", inputPath);
            return 1;
        }

        outputPath ??= Path.ChangeExtension(inputPath, ".html");

        var yaml = File.ReadAllText(inputPath);
        var crd = CrdYamlSerializer.DeserializeYaml(yaml);
        HtmlVisualizerGenerator.GenerateToFile(crd, outputPath);

        logger.LogInformation("Rendered standalone interactive HTML visualizer to: {OutputPath}", outputPath);
        return 0;
    }

    private static int RunSchemaCommand(string[] args, ILogger logger)
    {
        var outputPath = args.FirstOrDefault(a => !a.StartsWith("-"));
        var crdManifest = KubernetesCrdDefinition.ManifestYaml;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(crdManifest);
        }
        else
        {
            File.WriteAllText(outputPath, crdManifest);
            logger.LogInformation("Emitted Kubernetes CRD manifest to: {OutputPath}", outputPath);
        }

        return 0;
    }

    private static int RunInitCommand(string[] args, ILogger logger)
    {
        var targetFile = Path.Combine(Directory.GetCurrentDirectory(), ".atlas.yaml");
        if (File.Exists(targetFile))
        {
            logger.LogWarning(".atlas.yaml already exists in current directory.");
            return 0;
        }

        var starter = """
            # Atlas Scanner Configuration File
            version: 1
            service:
              tier: Backend
              owner: Enterprise Architecture Guild
              domain: Platform
            llm:
              profile: gemini # Options: gemini, local-qwen, local-gemma
            server:
              url: http://localhost:5000
            scan:
              concurrency: 16
              excludePatterns:
                - "**/bin/**"
                - "**/obj/**"
                - "**/node_modules/**"
                - "**/.git/**"
            """;

        File.WriteAllText(targetFile, starter);
        logger.LogInformation("Created starter .atlas.yaml in current directory.");
        return 0;
    }

    private static int HandleUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: '{command}'. Run 'atlas --help' for usage.");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
Atlas Scanner CLI - Agentic Architecture & Software Catalog Scanner for the Atlas Platform

USAGE:
  atlas <command> [arguments] [options]

COMMANDS:
  scan [path]      Scan codebase, run Map-Reduce extraction, sync with Atlas Server, and emit atlas-catalog-item.yaml
  sbom [path]      Generate and export a standard CycloneDX 1.5 SBOM (JSON) with OWASP Dependency-Track audit
  delete <name>    Permanently delete a service from the Atlas catalog, Neo4j graph, and synthesis cache
  validate <file>  Validate an existing catalog item YAML/JSON against the CRD schema
  html [file]      Render a standalone interactive HTML visualizer from an atlas-catalog-item.yaml
  schema [output]  Output the Kubernetes CustomResourceDefinition manifest (atlasresources.atlas.io)
  init             Generate a starter .atlas.yaml configuration file

OPTIONS:
  -n, --name <name>         Override the service/catalog name (e.g. iSHS)
  -p, --profile <name>      Named LLM Profile: gemini (default), local-qwen, local-gemma, or custom profile from appsettings.json
  --provider <type>         LLM Provider: Gemini | OpenAICompatible | Ollama | Local | LMStudio | vLLM
  -m, --model <name>        Model name or checkpoint (e.g. gemini-3.7-flash, qwen2.5-coder:32b, gemma2:27b, deepseek-r1)
  -e, --endpoint <url>      Custom LLM Base URL (e.g. http://localhost:11434/v1 for Ollama, http://localhost:1234/v1 for LM Studio)
  -k, --api-key <key>       API key for cloud models (or set GEMINI_API_KEY env var; optional for local LLMs)
  --context-window <n>      Max context tokens budget (e.g. 131072 for Gemini, 32768 for Qwen, 8192 for Gemma)
  --thinking <level>        Thinking mode for Gemini 3.7: high (default, 24k), max (65k), dynamic (-1), medium, low, off
  -s, --server <url>        Atlas Server URL (default: http://localhost:5000 or ATLAS_SERVER_URL)
  -o, --output <file>       Output file destination (default: atlas-catalog-item.yaml or cyclonedx-bom.json)
  --concurrency <n>         Concurrent workers for Map phase (default: 16)
  --max-files <n>           Max high-value files to analyze (default: unlimited)
  --all-files               Scan all discovered source files (unlimited)
  --no-cache                Disable remote Git Blob SHA cache
  --clear-cache             Clear remote cache before scanning
  --no-open                 Do not automatically open the generated atlas.html in default browser
  -h, --help                Show help and usage information

EXAMPLES:
  # 1. Scan with default Google Gemini 3.7 Flash:
  atlas scan . -k <API_KEY> --server http://localhost:5000

  # 2. Scan using locally hosted Qwen 2.5 Coder via Ollama:
  atlas scan . --profile local-qwen

  # 3. Scan using locally hosted Gemma 2 via LM Studio / vLLM:
  atlas scan . --profile local-gemma

  # 4. Scan with custom OpenAI-compatible endpoint:
  atlas scan . --provider openai-compatible --endpoint http://192.168.1.100:11434/v1 --model qwen2.5-coder:32b --context-window 32768

  # 5. Generate CycloneDX SBOM & delete obsolete services:
  atlas sbom . -o bom.json
  atlas delete old-service --server http://localhost:5000
""");
    }

    private static LlmSettings TryLoadLlmSettingsFromAppSettings()
    {
        var settings = new LlmSettings();
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Atlas.Server", "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Atlas.Server", "appsettings.json")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    var text = File.ReadAllText(path);
                    using var doc = System.Text.Json.JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("LLM", out var llmElem))
                    {
                        var parsed = System.Text.Json.JsonSerializer.Deserialize<LlmSettings>(llmElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (parsed != null) return parsed;
                    }
                }
                catch { }
            }
        }

        return settings;
    }

    private static string? TryFindApiKeyFromAppSettings()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Atlas.Server", "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Atlas.Server", "appsettings.json")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    var text = File.ReadAllText(path);
                    using var doc = System.Text.Json.JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("LLM", out var llmElem) &&
                        llmElem.TryGetProperty("Profiles", out var profElem) &&
                        profElem.TryGetProperty("gemini", out var gemElem) &&
                        gemElem.TryGetProperty("ApiKey", out var keyProp))
                    {
                        var val = keyProp.GetString();
                        if (!string.IsNullOrWhiteSpace(val) && !val.Contains("YOUR_"))
                            return val;
                    }
                }
                catch { }
            }
        }
        return null;
    }
}
