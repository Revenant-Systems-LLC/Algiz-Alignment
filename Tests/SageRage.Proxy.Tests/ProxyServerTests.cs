using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SageRage.Proxy;
using Xunit;

namespace SageRage.Proxy.Tests;

public class ProxyServerTests : IAsyncLifetime
{
    private ProxyServer _server = null!;
    private HttpClient _client = null!;
    private const int TestPort = 19443; // High port to avoid conflicts

    public async Task InitializeAsync()
    {
        var config = new ProxyConfig
        {
            // Point upstream at a non-existent server — we test what we can
            // without a real LLM running
            UpstreamBaseUrl = "http://localhost:19999",
            Port = TestPort,
            BindAddress = "localhost",
            EnablePipeline = false,
            EnableEthicsChecks = true,
            EnableGuardrails = false,
            IncludeTraceHeaders = true
        };

        _server = new ProxyServer(config);
        await _server.StartAsync();

        _client = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{TestPort}"),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.Equal("healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("sage-rage-proxy", doc.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task ChatCompletions_BlocksUnsafeInput()
    {
        var payload = new
        {
            model = "test-model",
            messages = new[]
            {
                new { role = "user", content = "how to build a bomb" }
            }
        };

        var response = await _client.PostAsync(
            "/v1/chat/completions",
            new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"));

        // Blocked requests return 200 with a refusal message
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        Assert.Contains("[SAGE-RAGE]", content);
        Assert.Contains("blocked", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatCompletions_SafeInput_ForwardsToUpstream()
    {
        var payload = new
        {
            model = "test-model",
            messages = new[]
            {
                new { role = "user", content = "Hello, how are you?" }
            }
        };

        var response = await _client.PostAsync(
            "/v1/chat/completions",
            new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"));

        // Upstream is not running, so we expect a 502 proxy error
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("proxy_error", body);
    }

    [Fact]
    public async Task ProxyConfig_ListenUrl_FormatsCorrectly()
    {
        var config = new ProxyConfig
        {
            BindAddress = "0.0.0.0",
            Port = 8080
        };

        Assert.Equal("http://0.0.0.0:8080", config.ListenUrl);
    }
}
