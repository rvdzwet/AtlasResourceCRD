using System.IO;
using AtlasResourceCRD.Core.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtlasResourceCRD.Tests;

public class CachingTests
{
    [Fact]
    public void ComputeBlobSha_ShouldMatchGitStandardFormat()
    {
        // "hello world\n" in git is blob 12
        const string text = "hello world\n";
        var sha = GitBlobShaCalculator.ComputeBlobShaForText(text);

        // Standard Git SHA1 for "hello world\n"
        sha.Should().Be("3b18e512dba79e4c8300dd08aeb37f8e728b8dad");
    }

    [Fact]
    public void FileSummaryCache_ShouldStoreAndRetrieve()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AtlasCacheTest_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var cache = new FileSummaryCache(tempDir, NullLogger<FileSummaryCache>.Instance);
            var summary = new FileSummary
            {
                RelativePath = "src/Controllers/ItemController.cs",
                GitBlobSha = "3b18e512dba79e4c8300dd08aeb37f8e728b8dad",
                Category = "Controller",
                Purpose = "Handles IoT item endpoints",
                EndpointsOrRoutes = new System.Collections.Generic.List<string> { "GET /api/items" }
            };

            cache.Store(summary);

            var retrieved = cache.TryGet("3b18e512dba79e4c8300dd08aeb37f8e728b8dad");
            retrieved.Should().NotBeNull();
            retrieved!.RelativePath.Should().Be("src/Controllers/ItemController.cs");
            retrieved.EndpointsOrRoutes.Should().Contain("GET /api/items");

            cache.CacheHits.Should().Be(1);

            var miss = cache.TryGet("nonexistent_sha");
            miss.Should().BeNull();
            cache.CacheMisses.Should().Be(1);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
