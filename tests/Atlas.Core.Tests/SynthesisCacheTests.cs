using System;
using System.Collections.Generic;
using System.IO;
using Atlas.Core.Caching;
using Atlas.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Tests;

public class SynthesisCacheTests
{
    [Fact]
    public void ComputeDiff_ShouldCorrectlyIdentifyAddedModifiedDeletedAndUnchangedFiles()
    {
        var cachedShas = new Dictionary<string, string>
        {
            ["file1.cs"] = "sha_1_old",
            ["file2.cs"] = "sha_2_same",
            ["file3_deleted.cs"] = "sha_3"
        };

        var currentShas = new Dictionary<string, string>
        {
            ["file1.cs"] = "sha_1_new", // Modified
            ["file2.cs"] = "sha_2_same", // Unchanged
            ["file4_added.cs"] = "sha_4" // Added
        };

        var diff = SynthesisCache.ComputeDiff(currentShas, cachedShas);

        diff.HasChanges.Should().BeTrue();
        diff.AddedFiles.Should().ContainSingle().Which.Should().Be("file4_added.cs");
        diff.ModifiedFiles.Should().ContainSingle().Which.Should().Be("file1.cs");
        diff.DeletedFiles.Should().ContainSingle().Which.Should().Be("file3_deleted.cs");
        diff.UnchangedFiles.Should().ContainSingle().Which.Should().Be("file2.cs");
    }

    [Fact]
    public void SynthesisCache_StoreAndRetrieve_ShouldMatchExactCommitAndShas()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "atlas_synth_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var cache = new SynthesisCache(tempDir, NullLogger<SynthesisCache>.Instance);

            var shas = new Dictionary<string, string>
            {
                ["src/Program.cs"] = "blob_sha_12345"
            };

            var entry = new SynthesisCacheEntry
            {
                GitCommit = "abc1234",
                GitBranch = "master",
                FileShaMap = shas,
                Resource = new AtlasResource
                {
                    Metadata = new AtlasResourceMetadata { Name = "test-service" },
                    Spec = new AtlasResourceSpec
                    {
                        ComponentOverview = new ComponentOverview { Name = "test-service", Tier = "Backend" }
                    }
                },
                CachedHtml = "<html>test</html>"
            };

            cache.Store(entry);

            var hit = cache.TryGetExactMatch("abc1234", shas);
            hit.Should().NotBeNull();
            hit!.Resource.Metadata.Name.Should().Be("test-service");
            hit.CachedHtml.Should().Be("<html>test</html>");

            // Different commit or modified SHA should be a cache miss
            var missShas = new Dictionary<string, string>
            {
                ["src/Program.cs"] = "blob_sha_DIFFERENT"
            };
            var miss = cache.TryGetExactMatch("abc1234", missShas);
            miss.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
