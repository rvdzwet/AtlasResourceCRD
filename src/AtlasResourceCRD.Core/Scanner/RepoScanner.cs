using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AtlasResourceCRD.Core.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AtlasResourceCRD.Core.Scanner;

public sealed class RepoScanner
{
    private readonly ILogger<RepoScanner> _logger;
    private readonly ManifestAnalyzer _manifestAnalyzer;
    private readonly GitMetadataExtractor _gitExtractor;

    private static readonly HashSet<string> DefaultIgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", ".vs", ".idea", ".vscode", "dist", "build",
        "out", ".terraform", "coverage", ".gemini", "target", "vendor", "__pycache__", ".pytest_cache"
    };

    private static readonly HashSet<string> DefaultIgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".so", ".dylib", ".zip", ".tar", ".gz", ".7z",
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".woff", ".woff2", ".ttf", ".eot",
        ".mp4", ".mov", ".avi", ".pdf", ".lock"
    };

    public RepoScanner(
        ILogger<RepoScanner> logger,
        ManifestAnalyzer manifestAnalyzer,
        GitMetadataExtractor gitExtractor)
    {
        _logger = logger;
        _manifestAnalyzer = manifestAnalyzer;
        _gitExtractor = gitExtractor;
    }

    public CodebaseSkeleton Scan(string rootPath, AtlasConfig? explicitConfig = null, int? maxFilesOverride = null)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        _logger.LogInformation("[RepoScanner] Starting repository scan at: {RootPath}", fullRoot);

        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Target directory does not exist: {fullRoot}");
        }

        var skeleton = new CodebaseSkeleton
        {
            RootPath = fullRoot,
            RepoName = new DirectoryInfo(fullRoot).Name
        };

        // 1. Check for .atlas.yaml
        var atlasConfig = explicitConfig ?? LoadAtlasConfig(fullRoot);
        skeleton.LocalConfig = atlasConfig;

        // 2. Load Git metadata
        skeleton.Git = _gitExtractor.Extract(fullRoot);

        // 3. Load .gitignore patterns
        var gitIgnorePatterns = LoadGitIgnore(fullRoot);
        _logger.LogDebug("[RepoScanner] Loaded {Count} .gitignore patterns", gitIgnorePatterns.Count);

        // 4. Traverse repository files
        var allScannedFiles = new List<ScannedFile>();
        ScanDirectory(fullRoot, fullRoot, gitIgnorePatterns, atlasConfig, allScannedFiles);

        skeleton.AllFiles = allScannedFiles;
        skeleton.TotalFiles = allScannedFiles.Count;
        skeleton.TotalSizeBytes = allScannedFiles.Sum(f => f.SizeBytes);

        _logger.LogInformation("[RepoScanner] Discovered {TotalFiles} files ({TotalSizeMb:F2} MB total)",
            skeleton.TotalFiles, skeleton.TotalSizeBytes / (1024.0 * 1024.0));

        // Group extensions
        foreach (var group in allScannedFiles.GroupBy(f => f.Extension, StringComparer.OrdinalIgnoreCase))
        {
            var ext = string.IsNullOrWhiteSpace(group.Key) ? "(no ext)" : group.Key;
            skeleton.ExtensionCounts[ext] = group.Count();
            _logger.LogTrace("[RepoScanner] Extension {Extension}: {Count} files", ext, group.Count());
        }

        // 5. Analyze manifests & README
        foreach (var file in allScannedFiles)
        {
            var fileName = Path.GetFileName(file.RelativePath);

            if (fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase) && skeleton.ReadmeContent == null)
            {
                try
                {
                    skeleton.ReadmeContent = File.ReadAllText(file.FullPath);
                    _logger.LogDebug("[RepoScanner] Loaded README.md ({Length} chars)", skeleton.ReadmeContent.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RepoScanner] Failed to read README.md");
                }
            }

            var manifest = _manifestAnalyzer.Analyze(file.RelativePath, file.FullPath);
            if (manifest != null)
            {
                skeleton.Manifests.Add(manifest);
                _logger.LogInformation("[RepoScanner] Identified manifest: {Type} at {RelativePath}", manifest.ManifestType, file.RelativePath);
            }
        }

        // 6. Select high-value source files for LLM analysis
        skeleton.HighValueFiles = SelectHighValueFiles(allScannedFiles, fullRoot, atlasConfig?.MaxFileSizeKb ?? 256, maxFilesOverride ?? atlasConfig?.MaxFiles ?? int.MaxValue);
        _logger.LogInformation("[RepoScanner] Selected {Count} high-value files for agentic analysis", skeleton.HighValueFiles.Count);

        return skeleton;
    }

    private void ScanDirectory(
        string currentDir,
        string rootDir,
        List<string> gitIgnorePatterns,
        AtlasConfig? config,
        List<ScannedFile> result)
    {
        var dirInfo = new DirectoryInfo(currentDir);
        var dirName = dirInfo.Name;

        if (DefaultIgnoredDirs.Contains(dirName))
        {
            _logger.LogTrace("[RepoScanner] Skipping ignored directory: {Dir}", currentDir);
            return;
        }

        var relDir = Path.GetRelativePath(rootDir, currentDir).Replace('\\', '/');
        if (relDir != "." && IsIgnored(relDir, gitIgnorePatterns, config))
        {
            _logger.LogTrace("[RepoScanner] Skipping gitignored directory: {Dir}", relDir);
            return;
        }

        try
        {
            foreach (var fileInfo in dirInfo.GetFiles())
            {
                var relPath = Path.GetRelativePath(rootDir, fileInfo.FullName).Replace('\\', '/');
                var ext = fileInfo.Extension;

                if (DefaultIgnoredExtensions.Contains(ext))
                {
                    _logger.LogTrace("[RepoScanner] Skipping ignored extension: {File}", relPath);
                    continue;
                }

                if (IsIgnored(relPath, gitIgnorePatterns, config))
                {
                    _logger.LogTrace("[RepoScanner] Skipping ignored file: {File}", relPath);
                    continue;
                }

                result.Add(new ScannedFile
                {
                    RelativePath = relPath,
                    FullPath = fileInfo.FullName,
                    SizeBytes = fileInfo.Length,
                    Extension = ext.ToLowerInvariant()
                });
            }

            foreach (var subDir in dirInfo.GetDirectories())
            {
                ScanDirectory(subDir.FullName, rootDir, gitIgnorePatterns, config, result);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "[RepoScanner] Access denied to directory: {Dir}", currentDir);
        }
    }

    private static bool IsIgnored(string relativePath, List<string> gitIgnorePatterns, AtlasConfig? config)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');

        if (config?.IgnoreGlobs != null)
        {
            foreach (var glob in config.IgnoreGlobs)
            {
                if (MatchesGlob(normalized, glob))
                    return true;
            }
        }

        foreach (var pattern in gitIgnorePatterns)
        {
            if (MatchesGlob(normalized, pattern))
                return true;
        }

        return false;
    }

    private static bool MatchesGlob(string path, string glob)
    {
        if (string.IsNullOrWhiteSpace(glob)) return false;
        var cleanGlob = glob.Trim();

        var isDirOnly = cleanGlob.EndsWith("/");
        cleanGlob = cleanGlob.TrimEnd('/');

        var regexPattern = "^" + Regex.Escape(cleanGlob)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/]*")
            .Replace(@"\?", ".") + (isDirOnly ? "(/.*)?$" : "$");

        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }

    private static List<string> LoadGitIgnore(string root)
    {
        var gitIgnorePath = Path.Combine(root, ".gitignore");
        if (!File.Exists(gitIgnorePath))
            return new List<string>();

        return File.ReadAllLines(gitIgnorePath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .ToList();
    }

    private AtlasConfig? LoadAtlasConfig(string root)
    {
        var paths = new[]
        {
            Path.Combine(root, ".atlas.yaml"),
            Path.Combine(root, ".atlas.yml"),
            Path.Combine(root, "atlas.yaml")
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    _logger.LogInformation("[RepoScanner] Found configuration file: {Path}", path);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();
                    return deserializer.Deserialize<AtlasConfig>(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RepoScanner] Failed to parse {Path}", path);
                }
            }
        }

        return null;
    }

    private List<ScannedSourceFile> SelectHighValueFiles(List<ScannedFile> files, string root, int maxFileSizeKb, int maxFiles)
    {
        var highValue = new List<ScannedSourceFile>();
        var maxBytes = maxFileSizeKb * 1024;

        foreach (var file in files)
        {
            if (file.SizeBytes > maxBytes) continue;

            var rel = file.RelativePath;
            var fileName = Path.GetFileName(rel);
            var category = CategorizeFile(rel, fileName);

            if (category != null)
            {
                try
                {
                    var content = File.ReadAllText(file.FullPath);
                    highValue.Add(new ScannedSourceFile
                    {
                        RelativePath = rel,
                        Category = category,
                        Content = content
                    });
                    _logger.LogTrace("[RepoScanner] Categorized high-value file '{Path}' as '{Category}'", rel, category);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "[RepoScanner] Skipped unreadable file {Path}", rel);
                }
            }
        }

        if (maxFiles < int.MaxValue)
        {
            return highValue.OrderBy(f => PriorityScore(f.Category)).Take(maxFiles).ToList();
        }

        return highValue.OrderBy(f => PriorityScore(f.Category)).ToList();
    }

    private static int PriorityScore(string category) => category switch
    {
        "ArchitectureDoc" => 1,
        "EntryPoint" => 2,
        "Dockerfile" => 3,
        "Workflow" => 4,
        "Controller" => 5,
        "RuleEngine" => 6,
        "Provider" => 7,
        "Service" => 8,
        "Client" => 9,
        "Repository" => 10,
        "Worker" => 11,
        "Schema" => 12,
        "Config" => 13,
        _ => 20
    };

    private static string? CategorizeFile(string rel, string fileName)
    {
        var lowerRel = rel.ToLowerInvariant();
        var lowerName = fileName.ToLowerInvariant();

        // Skip generated files, tests (unless specified), designer files, migrations
        if (lowerName.EndsWith(".designer.cs") || lowerName.EndsWith(".g.cs") || lowerName.EndsWith(".g.i.cs") ||
            lowerName.Contains("assemblyattributes") || lowerRel.Contains("/migrations/") || lowerRel.Contains("\\migrations\\") ||
            lowerRel.Contains(".tests/") || lowerRel.Contains(".tests\\") || lowerRel.Contains("/test/") || lowerRel.Contains("\\test\\"))
        {
            return null;
        }

        if (lowerName.EndsWith(".md") && (lowerName.Contains("arch") || lowerName.Contains("design") || lowerName.Contains("spec") || lowerName == "readme.md"))
            return "ArchitectureDoc";

        if (lowerName.StartsWith("dockerfile") || lowerName.StartsWith("docker-compose"))
            return "Dockerfile";

        if (lowerRel.StartsWith(".github/workflows/") || lowerRel.StartsWith(".gitlab-ci") || lowerRel.Contains("jenkinsfile"))
            return "Workflow";

        if (lowerName == "program.cs" || lowerName == "startup.cs" || lowerName == "main.go" || lowerName == "main.rs" || lowerName == "index.ts" || lowerName == "app.ts" || lowerName == "main.py" || lowerName == "app.py" || lowerName == "server.ts")
            return "EntryPoint";

        if (lowerName.Contains("controller") || lowerName.Contains("route") || lowerName.Contains("endpoint") || lowerName.Contains("handler") || lowerName.Contains("api") || lowerName.Contains("hub") || lowerName.Contains("resolver"))
            return "Controller";

        if (lowerName.Contains("provider") || lowerName.Contains("plugin") || lowerName.Contains("adapter") || lowerName.Contains("driver") || lowerName.Contains("connector") || lowerName.Contains("bridge") || lowerName.Contains("integration"))
            return "Provider";

        if (lowerName.Contains("rule") || lowerName.Contains("engine") || lowerName.Contains("brain") || lowerName.Contains("muscle") || lowerName.Contains("evaluator") || lowerName.Contains("policy"))
            return "RuleEngine";

        if (lowerName.Contains("service") || lowerName.Contains("manager") || lowerName.Contains("orchestrator") || lowerName.Contains("processor") || lowerName.Contains("facade"))
            return "Service";

        if (lowerName.Contains("client") || lowerName.Contains("gateway") || lowerName.Contains("channel") || lowerName.Contains("proxy"))
            return "Client";

        if (lowerName.Contains("repository") || lowerName.Contains("dbcontext") || lowerName.Contains("store") || lowerName.Contains("dao") || lowerName.Contains("sink"))
            return "Repository";

        if (lowerName.Contains("worker") || lowerName.Contains("job") || lowerName.Contains("consumer") || lowerName.Contains("listener") || lowerName.Contains("subscriber") || lowerName.Contains("hostedservice"))
            return "Worker";

        if (lowerName.Contains("schema") || lowerName.Contains("model") || lowerName.Contains("entity") || lowerName.Contains("dto") || lowerName.Contains("contract") || lowerName.Contains("event") || lowerName.Contains("message"))
            return "Schema";

        if (lowerName.Contains("appsettings") || lowerName.EndsWith(".env.example") || lowerName.Contains("config") || lowerName.Contains("settings"))
            return "Config";

        // General code files
        var ext = Path.GetExtension(lowerName);
        if (ext is ".cs" or ".ts" or ".js" or ".py" or ".go" or ".rs" or ".java" or ".kt" or ".cpp" or ".c" or ".sql")
            return "Source";

        return null;
    }
}
