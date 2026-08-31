using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Caching;
using Atlas.Core.Client;
using Atlas.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Core.Tests;

public class ServerAndRemoteCacheTests
{
    [Fact]
    public async Task AtlasServerClient_CheckSynthesis_ReturnsExactMatch_WhenServerResponds200()
    {
        var expectedResource = new AtlasResource
        {
            Metadata = new AtlasResourceMetadata { Name = "test-service" }
        };

        var handler = new MockHttpMessageHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/cache/synthesis/check") == true)
            {
                var response = new SynthesisCheckResponse
                {
                    IsExactMatch = true,
                    CachedResource = expectedResource
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(response)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new AtlasServerClient(new HttpClient(handler), NullLogger<AtlasServerClient>.Instance);
        var result = await client.CheckSynthesisAsync(
            "http://localhost:5000",
            "test-repo",
            "commit123",
            new Dictionary<string, string> { ["file.cs"] = "sha1" });

        result.Should().NotBeNull();
        result!.IsExactMatch.Should().BeTrue();
        result.CachedResource?.Metadata?.Name.Should().Be("test-service");
    }

    [Fact]
    public async Task AtlasServerClient_QueryFileSummaries_ReturnsBulkSummaries()
    {
        var handler = new MockHttpMessageHandler(async request =>
        {
            var response = new FileSummaryQueryResponse
            {
                Summaries = new Dictionary<string, FileSummary>
                {
                    ["sha1"] = new FileSummary { RelativePath = "Service.cs", Purpose = "Core logic" }
                }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            };
        });

        var client = new AtlasServerClient(new HttpClient(handler), NullLogger<AtlasServerClient>.Instance);
        var result = await client.QueryFileSummariesAsync("http://localhost:5000", new List<string> { "sha1", "sha2" });

        result.Should().ContainKey("sha1");
        result["sha1"].RelativePath.Should().Be("Service.cs");
    }

    [Fact]
    public async Task AtlasServerClient_IngestCatalogItem_PostsPayloadSuccessfully()
    {
        var handler = new MockHttpMessageHandler(async request =>
        {
            var response = new CatalogIngestResponse
            {
                Success = true,
                Message = "Ingested successfully",
                GraphUpdated = true,
                ResourceName = "atlas-hub"
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            };
        });

        var client = new AtlasServerClient(new HttpClient(handler), NullLogger<AtlasServerClient>.Instance);
        var result = await client.IngestCatalogItemAsync(
            "http://localhost:5000",
            new AtlasResource { Metadata = new AtlasResourceMetadata { Name = "atlas-hub" } },
            "commit-abc",
            new Dictionary<string, string>());

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ResourceName.Should().Be("atlas-hub");
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
