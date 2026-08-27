using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AtlasResourceCRD.Core.Models;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Core.Scanner;

public sealed class ManifestAnalyzer
{
    private readonly ILogger<ManifestAnalyzer> _logger;

    public ManifestAnalyzer(ILogger<ManifestAnalyzer> logger)
    {
        _logger = logger;
    }

    public ScannedManifest? Analyze(string relativePath, string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath).ToLowerInvariant();

        try
        {
            var content = File.ReadAllText(fullPath);
            _logger.LogTrace("[ManifestAnalyzer] Analyzing manifest file: {RelativePath} ({Size} chars)", relativePath, content.Length);

            if (extension == ".csproj" || extension == ".fsproj")
            {
                return AnalyzeCsproj(relativePath, content);
            }

            if (fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzePackageJson(relativePath, content);
            }

            if (fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) || fileName.Equals("Pipfile", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzePythonRequirements(relativePath, content);
            }

            if (fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzePyProjectToml(relativePath, content);
            }

            if (fileName.Equals("go.mod", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzeGoMod(relativePath, content);
            }

            if (fileName.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzeCargoToml(relativePath, content);
            }

            if (fileName.StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzeDockerfile(relativePath, content);
            }

            if (fileName.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzeDockerCompose(relativePath, content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ManifestAnalyzer] Failed to parse manifest file: {RelativePath}", relativePath);
        }

        return null;
    }

    private ScannedManifest AnalyzeCsproj(string relativePath, string content)
    {
        var manifest = new ScannedManifest
        {
            RelativePath = relativePath,
            ManifestType = "DotNetCsproj",
            RawContent = content
        };

        try
        {
            var doc = XDocument.Parse(content);
            var targetFramework = doc.Root?.Element("PropertyGroup")?.Element("TargetFramework")?.Value
                                  ?? doc.Root?.Element("PropertyGroup")?.Element("TargetFrameworks")?.Value;

            manifest.TargetRuntime = targetFramework;
            _logger.LogDebug("[ManifestAnalyzer] .NET csproj {Path}: TargetFramework={TargetFramework}", relativePath, targetFramework);

            foreach (var packageRef in doc.Descendants("PackageReference"))
            {
                var name = packageRef.Attribute("Include")?.Value ?? packageRef.Attribute("Update")?.Value;
                var version = packageRef.Attribute("Version")?.Value ?? packageRef.Element("Version")?.Value;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    manifest.ExtractedPackages.Add(new PackageDependency
                    {
                        Name = name,
                        Version = version
                    });
                    _logger.LogTrace("[ManifestAnalyzer] Extracted NuGet package: {Name} (Version {Version})", name, version);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ManifestAnalyzer] XML parse error in {Path}", relativePath);
        }

        return manifest;
    }

    private ScannedManifest AnalyzePackageJson(string relativePath, string content)
    {
        var manifest = new ScannedManifest
        {
            RelativePath = relativePath,
            ManifestType = "NodePackageJson",
            RawContent = content,
            TargetRuntime = "Node.js"
        };

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in deps.EnumerateObject())
                {
                    manifest.ExtractedPackages.Add(new PackageDependency
                    {
                        Name = prop.Name,
                        Version = prop.Value.GetString()
                    });
                    _logger.LogTrace("[ManifestAnalyzer] Extracted NPM dependency: {Name} ({Version})", prop.Name, prop.Value.GetString());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ManifestAnalyzer] JSON parse error in {Path}", relativePath);
        }

        return manifest;
    }

    private ScannedManifest AnalyzePythonRequirements(string relativePath, string content)
    {
        var manifest = new ScannedManifest
        {
            RelativePath = relativePath,
            ManifestType = "PythonRequirements",
            RawContent = content,
            TargetRuntime = "Python"
        };

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            var match = Regex.Match(line, @"^([a-zA-Z0-9_\-\.]+)(?:[=><~^!]+\s*([a-zA-Z0-9_\-\.]+))?");
            if (match.Success)
            {
                manifest.ExtractedPackages.Add(new PackageDependency
                {
                    Name = match.Groups[1].Value,
                    Version = match.Groups[2].Success ? match.Groups[2].Value : null
                });
            }
        }

        return manifest;
    }

    private ScannedManifest AnalyzePyProjectToml(string relativePath, string content)
    {
        var manifest = new ScannedManifest
        {
            RelativePath = relativePath,
            ManifestType = "PyProjectToml",
            RawContent = content,
            TargetRuntime = "Python"
        };

        // Extract dependencies regex
        var depMatches = Regex.Matches(content, @"(?:dependencies|requires)\s*=\s*\[(.*?)\]", RegexOptions.Singleline);
        foreach (Match match in depMatches)
        {
            var listContent = match.Groups[1].Value;
            var items = Regex.Matches(listContent, @"""([^""]+)""|'([^']+)'");
            foreach (Match item in items)
            {
                var pkgStr = item.Groups[1].Success ? item.Groups[1].Value : item.Groups[2].Value;
                manifest.ExtractedPackages.Add(new PackageDependency { Name = pkgStr });
            }
        }

        return manifest;
    }

    private ScannedManifest AnalyzeGoMod(string relativePath, string content)
    {
        var manifest = new ScannedManifest
        {
            RelativePath = relativePath,
            ManifestType = "GoMod",
            RawContent = content,
            TargetRuntime = "Go"
        };

        var goVersionMatch = Regex.Match(content, @"^go\s+([0-9\.]+)", RegexOptions.Multiline);
        if (goVersionMatch.Success)
        {
            manifest.TargetRuntime = $"Go {goVersionMatch.Groups[1].Value}";
        }

        var reqMatches = Regex.Matches(content, @"^\t([a-zA-Z0-9\.\-_/]+)\s+([a-zA-Z0-9\.\-_+]+)", RegexOptions.Multiline);
        foreach (Match m in reqMatches)
        {
            manifest.ExtractedPackages.Add(new PackageDependency
            {
                Name = m.Groups[1].Value,
                Version = m.Groups[2].Value
            });
        }

        return manifest;
    }

    private ScannedManifest AnalyzeCargoToml(string relativePath, string content)
    {
        var manifest = new ScannedManifest
        {
            RelativePath = relativePath,
            ManifestType = "CargoToml",
            RawContent = content,
            TargetRuntime = "Rust"
        };

        var depSectionMatch = Regex.Match(content, @"\[dependencies\](.*?)(?:\[|\z)", RegexOptions.Singleline);
        if (depSectionMatch.Success)
        {
            var depLines = depSectionMatch.Groups[1].Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in depLines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                var m = Regex.Match(line, @"^([a-zA-Z0-9_\-]+)\s*=\s*(?:""([^""]+)""|\{.*version\s*=\s*""([^""]+)""|.*)");
                if (m.Success)
                {
                    manifest.ExtractedPackages.Add(new PackageDependency
                    {
                        Name = m.Groups[1].Value,
                        Version = m.Groups[2].Success ? m.Groups[2].Value : (m.Groups[3].Success ? m.Groups[3].Value : null)
                    });
                }
            }
        }

        return manifest;
    }

    private ScannedManifest AnalyzeDockerfile(string relativePath, string content)
    {
        var manifest = new ScannedManifest
        {
            RelativePath = relativePath,
            ManifestType = "Dockerfile",
            RawContent = content,
            TargetRuntime = "Container / Docker"
        };

        // Detect ports
        var exposeMatches = Regex.Matches(content, @"^[ \t]*EXPOSE[ \t]+([0-9 /a-zA-Z]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        foreach (Match m in exposeMatches)
        {
            var ports = m.Groups[1].Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            manifest.ExposedPorts.AddRange(ports);
            _logger.LogTrace("[ManifestAnalyzer] Dockerfile {Path} exposes port: {Ports}", relativePath, m.Groups[1].Value);
        }

        // Detect ENV vars
        var envMatches = Regex.Matches(content, @"^[ \t]*ENV[ \t]+([a-zA-Z0-9_]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        foreach (Match m in envMatches)
        {
            manifest.EnvironmentVariables.Add(m.Groups[1].Value);
            _logger.LogTrace("[ManifestAnalyzer] Dockerfile {Path} env var: {Env}", relativePath, m.Groups[1].Value);
        }

        return manifest;
    }

    private ScannedManifest AnalyzeDockerCompose(string relativePath, string content)
    {
        var manifest = new ScannedManifest
        {
            RelativePath = relativePath,
            ManifestType = "DockerCompose",
            RawContent = content,
            TargetRuntime = "Docker Compose"
        };

        // Extract services and ports loosely
        var serviceMatches = Regex.Matches(content, @"^\s{2}([a-zA-Z0-9_\-]+):", RegexOptions.Multiline);
        foreach (Match m in serviceMatches)
        {
            var serviceName = m.Groups[1].Value;
            if (serviceName != "version" && serviceName != "services" && serviceName != "networks" && serviceName != "volumes")
            {
                manifest.ExtractedPackages.Add(new PackageDependency
                {
                    Name = $"Service: {serviceName}",
                    Purpose = "Docker Compose Service"
                });
            }
        }

        return manifest;
    }
}
