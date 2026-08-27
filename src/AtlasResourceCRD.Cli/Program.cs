using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AtlasResourceCRD.Core.Agents;
using AtlasResourceCRD.Core.Gemini;
using AtlasResourceCRD.Core.Html;
using AtlasResourceCRD.Core.Models;
using AtlasResourceCRD.Core.Scanner;
using AtlasResourceCRD.Core.Schema;
using AtlasResourceCRD.Core.Serialization;
using AtlasResourceCRD.Core.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Cli;

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

        var logger = loggerFactory.CreateLogger("AtlasResourceCRD.Cli");

        try
        {
            return command switch
            {
                "scan" => await RunScanCommand(cmdArgs, loggerFactory, logger),
                "validate" => RunValidateCommand(cmdArgs, logger),
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
        var dryRun = false;
        var model = "gemini-3.7-flash";
        var thinkingLevel = "high";
        var format = "yaml";
        var disableCache = false;
        var clearCache = false;
        var noOpen = false;
        var concurrency = 8;
        var maxFiles = int.MaxValue;
        string? endpoint = Environment.GetEnvironmentVariable("GEMINI_ENDPOINT");
        string? apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                         ?? Environment.GetEnvironmentVariable("ATLAS_API_KEY")
                         ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-o" or "--output" && i + 1 < args.Length)
            {
                outputFile = args[++i];
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
            else if (arg is "--no-open")
            {
                noOpen = true;
            }
            else if (arg is "--format" && i + 1 < args.Length)
            {
                format = args[++i].ToLowerInvariant();
            }
            else if (arg is "--dry-run" or "--test")
            {
                dryRun = true;
            }
            else if (!arg.StartsWith("-"))
            {
                targetPath = arg;
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogError("Gemini API key is required. Provide it via --api-key <KEY> or GEMINI_API_KEY environment variable.");
            return 1;
        }

        var fullPath = Path.GetFullPath(targetPath);
        var geminiOptions = new GeminiClientOptions
        {
            ApiKey = apiKey,
            Model = model
        };
        geminiOptions.ApplyThinkingLevel(thinkingLevel);
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            geminiOptions.BaseUrl = endpoint.TrimEnd('/');
        }

        logger.LogInformation("================================================================================");
        logger.LogInformation("AtlasResourceCRD Scanner - Starting Map-Reduce Scan for {Path}", fullPath);
        logger.LogInformation("Mode: {Mode} | Model: {Model} | Thinking: {ThinkingLevel} ({Tokens} tokens) | Concurrency: {Concurrency}",
            dryRun ? "TEST / DRY-RUN" : "STANDARD", model, thinkingLevel.ToUpperInvariant(), geminiOptions.ThinkingBudget, concurrency);
        logger.LogInformation("================================================================================");

        // 1. Pass 1: Static Discovery
        var gitExtractor = new GitMetadataExtractor(loggerFactory.CreateLogger<GitMetadataExtractor>());
        var manifestAnalyzer = new ManifestAnalyzer(loggerFactory.CreateLogger<ManifestAnalyzer>());
        var scanner = new RepoScanner(loggerFactory.CreateLogger<RepoScanner>(), manifestAnalyzer, gitExtractor);

        var skeleton = scanner.Scan(fullPath, maxFilesOverride: maxFiles);

        // 2. Pass 2: Map-Reduce Agentic Analysis
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(4) };
        var geminiClient = new GeminiClient(httpClient, geminiOptions, loggerFactory.CreateLogger<GeminiClient>());
        var summaryAgent = new FileSummaryAgent(geminiClient, loggerFactory.CreateLogger<FileSummaryAgent>());
        var pipeline = new AtlasAgentPipeline(geminiClient, summaryAgent, loggerFactory.CreateLogger<AtlasAgentPipeline>(), loggerFactory);

        if (clearCache)
        {
            var cache = new Core.Caching.FileSummaryCache(fullPath, loggerFactory.CreateLogger<Core.Caching.FileSummaryCache>());
            cache.ClearCache();
        }

        var pipelineOptions = new AtlasAgentPipelineOptions
        {
            Concurrency = concurrency,
            DisableCache = disableCache
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

        // 4. Output Generation: Always write atlas.yaml and atlas.html
        var yamlOutput = CrdYamlSerializer.SerializeYaml(crd);
        var defaultYamlPath = string.IsNullOrWhiteSpace(outputFile) ? Path.Combine(Directory.GetCurrentDirectory(), "atlas.yaml") : Path.GetFullPath(outputFile);
        var htmlOutputPath = Path.ChangeExtension(defaultYamlPath, ".html");

        File.WriteAllText(defaultYamlPath, yamlOutput);
        logger.LogInformation("CRD manifest written to: {YamlPath}", defaultYamlPath);

        HtmlVisualizerGenerator.GenerateToFile(crd, htmlOutputPath);
        logger.LogInformation("Interactive HTML dashboard generated: {HtmlPath}", htmlOutputPath);

        logger.LogInformation("================================================================================");
        logger.LogInformation("AtlasResourceCRD scan complete! [Component: {Name}, Tier: {Tier}]",
            crd.Metadata.Name, crd.Spec.ComponentOverview.Tier);
        logger.LogInformation("================================================================================");

        // 5. Automatically open HTML in default browser
        if (!noOpen && File.Exists(htmlOutputPath))
        {
            try
            {
                logger.LogInformation("Opening interactive documentation in browser...");
                var psi = new ProcessStartInfo
                {
                    FileName = htmlOutputPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not automatically launch default browser for {Path}", htmlOutputPath);
            }
        }

        return 0;
    }

    private static int RunValidateCommand(string[] args, ILogger logger)
    {
        if (args.Length == 0)
        {
            logger.LogError("Usage: atlas-crd validate <PATH_TO_YAML_FILE>");
            return 1;
        }

        var filePath = Path.GetFullPath(args[0]);
        if (!File.Exists(filePath))
        {
            logger.LogError("File not found: {FilePath}", filePath);
            return 1;
        }

        logger.LogInformation("Validating AtlasResource manifest: {FilePath}", filePath);
        var content = File.ReadAllText(filePath);

        AtlasResource resource;
        try
        {
            resource = filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? CrdYamlSerializer.DeserializeJson(content)
                : CrdYamlSerializer.DeserializeYaml(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse YAML/JSON manifest.");
            return 1;
        }

        var validation = CrdValidator.Validate(resource);
        if (validation.IsValid)
        {
            logger.LogInformation("Validation SUCCESS! Resource '{Name}' (apiVersion: {ApiVersion}, kind: {Kind}) is valid.",
                resource.Metadata.Name, resource.ApiVersion, resource.Kind);
            return 0;
        }

        logger.LogError("Validation FAILED with {Count} errors:", validation.Errors.Count);
        foreach (var err in validation.Errors) logger.LogError("  [ERROR] {Error}", err);
        foreach (var warn in validation.Warnings) logger.LogWarning("  [WARN] {Warning}", warn);

        return 1;
    }

    private static int RunSchemaCommand(string[] args, ILogger logger)
    {
        var outputFile = args.FirstOrDefault(a => !a.StartsWith("-"));
        var yaml = KubernetesCrdDefinition.ManifestYaml.Trim();

        if (!string.IsNullOrWhiteSpace(outputFile))
        {
            File.WriteAllText(outputFile, yaml);
            logger.LogInformation("Kubernetes CRD definition written to {Path}", Path.GetFullPath(outputFile));
        }
        else
        {
            Console.WriteLine(yaml);
        }

        return 0;
    }

    private static int RunInitCommand(string[] args, ILogger logger)
    {
        var targetFile = Path.Combine(Directory.GetCurrentDirectory(), ".atlas.yaml");
        if (File.Exists(targetFile))
        {
            logger.LogWarning(".atlas.yaml already exists in the current directory.");
            return 0;
        }

        const string sampleConfig = """
# AtlasResource configuration for repository scanning
name: my-service
namespace: default
tier: Backend # Frontend | Backend | CLI | Library | Worker | Gateway
owner: platform-team

ignoreGlobs:
  - "legacy/**"
  - "docs/archive/**"

labels:
  team: core-platform
  environment: production

annotations:
  atlas.io/criticality: high
""";

        File.WriteAllText(targetFile, sampleConfig.Trim());
        logger.LogInformation("Created starter configuration at {Path}", targetFile);
        return 0;
    }

    private static int HandleUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
AtlasResourceCRD CLI - Agentic Codebase Architecture & Software Catalog Scanner

USAGE:
  atlas-crd <command> [arguments] [options]

COMMANDS:
  scan [path]      Scan a codebase, run Map-Reduce extraction, and generate atlas.yaml + atlas.html
  validate <file>  Validate an existing AtlasResource YAML/JSON against the CRD schema
  schema [output]  Output the Kubernetes CustomResourceDefinition manifest (atlasresources.atlas.io)
  init             Generate a starter .atlas.yaml configuration file

OPTIONS:
  -o, --output <file>       Output file destination for the CRD YAML (defaults to atlas.yaml)
  --dry-run, --test         Run in test / preview mode
  -m, --model <name>        Gemini model name (default: gemini-3.7-flash)
  -k, --api-key <key>       Google Gemini API key (or set GEMINI_API_KEY env var)
  -e, --endpoint <url>      Custom Gemini / proxy endpoint
  --thinking <level>        Thinking mode: high (default, 24k), max (65k), dynamic (-1), medium, low, off
  --thinking-budget <n>     Exact thinking token budget (e.g. 24576)
  --concurrency <n>         Concurrent workers for Map phase (default: 8)
  --max-files <n>           Max high-value files to analyze (default: unlimited)
  --all-files               Scan all discovered source files (unlimited)
  --no-cache                Disable Git Blob SHA cache
  --clear-cache             Clear existing .atlas/cache before scanning
  --no-open                 Do not automatically open the generated atlas.html in default browser
  --format <yaml|json>      Output format: yaml (default) or json
  -v, --verbose             Enable debug logging
  -vv, --trace              Enable extreme trace logging (full payloads, prompts, thinking)
  -h, --help                Show help and usage information

EXAMPLES:
  atlas-crd scan . --dry-run
  atlas-crd scan /path/to/repo -k <KEY> -m gemini-3.7-flash --thinking high -v
  atlas-crd validate atlas.yaml
  atlas-crd schema k8s/atlas-crd.yaml
""");
    }
}
