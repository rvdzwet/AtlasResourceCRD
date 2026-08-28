using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace AtlasResourceCRD.Core.Scanner;

public sealed class GitMetadataExtractor
{
    private readonly ILogger<GitMetadataExtractor> _logger;

    public GitMetadataExtractor(ILogger<GitMetadataExtractor> logger)
    {
        _logger = logger;
    }

    public GitInfo? Extract(string repoRoot)
    {
        _logger.LogDebug("[GitMetadataExtractor] Attempting to extract git metadata from: {RepoRoot}", repoRoot);

        var gitDir = Path.Combine(repoRoot, ".git");
        if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
        {
            _logger.LogDebug("[GitMetadataExtractor] No .git directory found at {RepoRoot}", repoRoot);
            return null;
        }

        var info = new GitInfo();

        // 1. Try running git CLI
        try
        {
            info.CommitSha = RunGitCommand(repoRoot, "rev-parse HEAD");
            if (!string.IsNullOrWhiteSpace(info.CommitSha))
            {
                info.CommitSha = info.CommitSha.Trim();
                info.CommitShaShort = info.CommitSha.Length >= 7 ? info.CommitSha[..7] : info.CommitSha;
            }

            info.Branch = RunGitCommand(repoRoot, "rev-parse --abbrev-ref HEAD")?.Trim();
            info.RemoteUrl = RunGitCommand(repoRoot, "config --get remote.origin.url")?.Trim();
            info.Author = RunGitCommand(repoRoot, "log -1 --pretty=format:%an")?.Trim();

            _logger.LogInformation("[GitMetadataExtractor] Discovered Git metadata: Branch={Branch}, Commit={CommitShaShort}, Remote={RemoteUrl}",
                info.Branch, info.CommitShaShort, info.RemoteUrl);

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GitMetadataExtractor] Failed to run git CLI, attempting filesystem fallback.");
        }

        // 2. Filesystem fallback
        try
        {
            var headPath = Path.Combine(gitDir, "HEAD");
            if (File.Exists(headPath))
            {
                var headContent = File.ReadAllText(headPath).Trim();
                if (headContent.StartsWith("ref: refs/heads/"))
                {
                    info.Branch = headContent.Substring("ref: refs/heads/".Length).Trim();
                    var refPath = Path.Combine(gitDir, "refs", "heads", info.Branch);
                    if (File.Exists(refPath))
                    {
                        info.CommitSha = File.ReadAllText(refPath).Trim();
                        info.CommitShaShort = info.CommitSha.Length >= 7 ? info.CommitSha[..7] : info.CommitSha;
                    }
                }
                else
                {
                    info.CommitSha = headContent;
                    info.CommitShaShort = info.CommitSha.Length >= 7 ? info.CommitSha[..7] : info.CommitSha;
                    info.Branch = "HEAD (detached)";
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GitMetadataExtractor] Error reading git metadata from filesystem.");
            return info;
        }
    }

    private static string? RunGitCommand(string workingDir, string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return null;

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (process.WaitForExit(3000))
        {
            var output = outputTask.GetAwaiter().GetResult();
            return process.ExitCode == 0 ? output : null;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ignore failure to terminate process
        }

        return null;
    }
}
