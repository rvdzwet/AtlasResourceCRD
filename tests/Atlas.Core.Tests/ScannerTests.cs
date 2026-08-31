using System.IO;
using Atlas.Core.Models;
using Atlas.Core.Scanner;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Tests;

public class ScannerTests
{
    private readonly RepoScanner _scanner;

    public ScannerTests()
    {
        var manifestAnalyzer = new ManifestAnalyzer(NullLogger<ManifestAnalyzer>.Instance);
        var gitExtractor = new GitMetadataExtractor(NullLogger<GitMetadataExtractor>.Instance);
        _scanner = new RepoScanner(NullLogger<RepoScanner>.Instance, manifestAnalyzer, gitExtractor);
    }

    [Fact]
    public void Scan_ShouldRespectIgnoreRulesAndDetectFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AtlasScanTest_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "bin"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));
        Directory.CreateDirectory(Path.Combine(tempDir, "ignored_dir"));

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "src", "Program.cs"), "public class Program { public static void Main() {} }");
            File.WriteAllText(Path.Combine(tempDir, "bin", "temp.dll"), "binary content");
            File.WriteAllText(Path.Combine(tempDir, "ignored_dir", "file.cs"), "code");
            File.WriteAllText(Path.Combine(tempDir, ".gitignore"), "ignored_dir/\n*.tmp");
            File.WriteAllText(Path.Combine(tempDir, "README.md"), "# Test Project\nArchitecture details here.");

            var skeleton = _scanner.Scan(tempDir);

            skeleton.Should().NotBeNull();
            skeleton.AllFiles.Should().Contain(f => f.RelativePath == "src/Program.cs");
            skeleton.AllFiles.Should().Contain(f => f.RelativePath == "README.md");
            skeleton.AllFiles.Should().NotContain(f => f.RelativePath.StartsWith("bin/"));
            skeleton.AllFiles.Should().NotContain(f => f.RelativePath.StartsWith("ignored_dir/"));
            skeleton.ReadmeContent.Should().Contain("# Test Project");
            skeleton.HighValueFiles.Should().Contain(f => f.RelativePath == "src/Program.cs" && f.Category == "EntryPoint");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
