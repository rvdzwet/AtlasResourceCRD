using System.Collections.Generic;
using Atlas.Core.Models;

namespace Atlas.Core.Scanner;

public sealed class CodebaseSkeleton
{
    public string RootPath { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public Dictionary<string, int> ExtensionCounts { get; set; } = new();
    public List<ScannedFile> AllFiles { get; set; } = new();
    public List<ScannedManifest> Manifests { get; set; } = new();
    public List<ScannedSourceFile> HighValueFiles { get; set; } = new();
    public string? ReadmeContent { get; set; }
    public GitInfo? Git { get; set; }
    public AtlasConfig? LocalConfig { get; set; }
}

public sealed class ScannedFile
{
    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Extension { get; set; } = string.Empty;
}

public sealed class ScannedSourceFile
{
    public string RelativePath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Controller, EntryPoint, Config, Schema, ArchitectureDoc, Dockerfile, Workflow
    public string Content { get; set; } = string.Empty;
}

public sealed class ScannedManifest
{
    public string RelativePath { get; set; } = string.Empty;
    public string ManifestType { get; set; } = string.Empty; // DotNetCsproj, PackageJson, PyProject, GoMod, CargoToml, Dockerfile, Compose, Kubernetes
    public string? TargetRuntime { get; set; }
    public List<PackageDependency> ExtractedPackages { get; set; } = new();
    public List<string> ExposedPorts { get; set; } = new();
    public List<string> EnvironmentVariables { get; set; } = new();
    public string RawContent { get; set; } = string.Empty;
}

public sealed class GitInfo
{
    public string? Branch { get; set; }
    public string? CommitSha { get; set; }
    public string? CommitShaShort { get; set; }
    public string? RemoteUrl { get; set; }
    public string? Author { get; set; }
}
