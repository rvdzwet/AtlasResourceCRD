using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Gemini;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Core.Tests;

public class LlmMultiModelTests
{
    [Fact]
    public void LlmSettings_ResolvesStandardProfilesCorrectly()
    {
        var settings = new LlmSettings();

        var gemini = settings.GetActiveProfile("gemini");
        Assert.Equal("Gemini", gemini.Provider);
        Assert.Equal("gemini-3.7-flash", gemini.Model);
        Assert.Equal(131072, gemini.ContextWindow);

        var qwen = settings.GetActiveProfile("local-qwen");
        Assert.Equal("OpenAICompatible", qwen.Provider);
        Assert.Equal("qwen2.5-coder:32b", qwen.Model);
        Assert.Equal(32768, qwen.ContextWindow);

        var gemma = settings.GetActiveProfile("local-gemma");
        Assert.Equal("OpenAICompatible", gemma.Provider);
        Assert.Equal("gemma2:27b", gemma.Model);
        Assert.Equal(8192, gemma.ContextWindow);
    }

    [Fact]
    public void LlmClientFactory_InstantiatesExpectedClientTypes()
    {
        var nullLoggerFactory = NullLoggerFactory.Instance;

        var geminiClient = LlmClientFactory.Create(new LlmProfileConfig
        {
            Provider = "Gemini",
            Model = "gemini-3.7-flash",
            ApiKey = "test-key"
        }, nullLoggerFactory);

        Assert.IsType<GeminiClient>(geminiClient);
        Assert.Equal("Gemini", geminiClient.ProviderName);
        Assert.Equal("gemini-3.7-flash", geminiClient.ModelName);
        Assert.Equal(131072, geminiClient.ContextWindowTokens);

        var qwenClient = LlmClientFactory.Create(new LlmProfileConfig
        {
            Provider = "OpenAICompatible",
            Model = "qwen2.5-coder:32b",
            BaseUrl = "http://localhost:11434/v1",
            ContextWindow = 32768
        }, nullLoggerFactory);

        Assert.IsType<OpenAiCompatibleLlmClient>(qwenClient);
        Assert.Equal("OpenAICompatible", qwenClient.ProviderName);
        Assert.Equal("qwen2.5-coder:32b", qwenClient.ModelName);
        Assert.Equal(32768, qwenClient.ContextWindowTokens);
    }

    [Fact]
    public async Task OpenAiCompatibleLlmClient_ParsesChatCompletionResponseAndCodeblocks()
    {
        var jsonPayload = "{\"componentOverview\":{\"name\":\"TestSvc\",\"tier\":\"Backend\"}}";
        var mockResponse = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = $"```json\n{jsonPayload}\n```"
                    },
                    finish_reason = "stop"
                }
            }
        });

        var handler = new MockHttpMessageHandler(mockResponse, HttpStatusCode.OK);
        var http = new HttpClient(handler);

        var client = new OpenAiCompatibleLlmClient(http, new LlmProfileConfig
        {
            Provider = "OpenAICompatible",
            Model = "qwen2.5-coder:32b",
            BaseUrl = "http://localhost:11434/v1"
        }, NullLogger.Instance);

        var result = await client.GenerateContentAsync("Generate spec", "You are an architect", enforceJson: true);
        Assert.Equal(jsonPayload, result);
    }

    [Fact]
    public void LlmClientExtensions_ExtractJson_HandlesVariousFormatting()
    {
        var raw1 = "```json\n{\"key\":\"value\"}\n```";
        Assert.Equal("{\"key\":\"value\"}", LlmClientExtensions.ExtractJson(raw1));

        var raw2 = "Here is your output:\n{\"key\":\"value\"}\nHope this helps!";
        Assert.Equal("{\"key\":\"value\"}", LlmClientExtensions.ExtractJson(raw2));

        var raw3 = "[{\"item\":\"one\"}]";
        Assert.Equal("[{\"item\":\"one\"}]", LlmClientExtensions.ExtractJson(raw3));
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
