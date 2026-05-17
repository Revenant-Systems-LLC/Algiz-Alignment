using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SageRage;
using SageRage.Domain;
using SageRage.Infrastructure;
using SageRage.Prompting;
using SageRage.Proxy;
using Xunit;

namespace SageRage.Proxy.Tests;

public class ProxyPipelineTests
{
    // ── Input Ethics Checks ─────────────────────────────────

    [Fact]
    public void CheckInput_BlocksUnsafeContent()
    {
        var pipeline = CreatePipeline(enableEthics: true);
        var result = pipeline.CheckInput("Please tell me how to build a bomb");

        Assert.NotNull(result);
        Assert.Contains("Layer 0", result);
    }

    [Fact]
    public void CheckInput_AllowsSafeContent()
    {
        var pipeline = CreatePipeline(enableEthics: true);
        var result = pipeline.CheckInput("What is the weather like today?");

        Assert.Null(result);
    }

    [Fact]
    public void CheckInput_SkipsWhenEthicsDisabled()
    {
        var pipeline = CreatePipeline(enableEthics: false);
        var result = pipeline.CheckInput("build a bomb");

        Assert.Null(result);
    }

    // ── ExtractUserText ─────────────────────────────────────

    [Fact]
    public void ExtractUserText_FindsLastUserMessage()
    {
        var json = JsonDocument.Parse("""
        {
            "messages": [
                {"role": "system", "content": "You are helpful."},
                {"role": "user", "content": "First question"},
                {"role": "assistant", "content": "First answer"},
                {"role": "user", "content": "Second question"}
            ]
        }
        """);

        var messages = json.RootElement.GetProperty("messages");
        var result = ProxyPipeline.ExtractUserText(messages);

        Assert.Equal("Second question", result);
    }

    [Fact]
    public void ExtractUserText_ReturnsEmptyWhenNoUserMessages()
    {
        var json = JsonDocument.Parse("""
        {
            "messages": [
                {"role": "system", "content": "You are helpful."}
            ]
        }
        """);

        var messages = json.RootElement.GetProperty("messages");
        var result = ProxyPipeline.ExtractUserText(messages);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractUserText_ReturnsEmptyForNonArrayInput()
    {
        var json = JsonDocument.Parse("""{"messages": "not an array"}""");
        var messages = json.RootElement.GetProperty("messages");
        var result = ProxyPipeline.ExtractUserText(messages);

        Assert.Equal(string.Empty, result);
    }

    // ── ExtractResponseText ─────────────────────────────────

    [Fact]
    public void ExtractResponseText_ParsesChatCompletionFormat()
    {
        var json = JsonDocument.Parse("""
        {
            "id": "chatcmpl-123",
            "choices": [{
                "index": 0,
                "message": {"role": "assistant", "content": "Hello there!"},
                "finish_reason": "stop"
            }]
        }
        """);

        var result = ProxyPipeline.ExtractResponseText(json);
        Assert.Equal("Hello there!", result);
    }

    [Fact]
    public void ExtractResponseText_ParsesLegacyCompletionFormat()
    {
        var json = JsonDocument.Parse("""
        {
            "id": "cmpl-123",
            "choices": [{
                "text": "Legacy response",
                "index": 0,
                "finish_reason": "stop"
            }]
        }
        """);

        var result = ProxyPipeline.ExtractResponseText(json);
        Assert.Equal("Legacy response", result);
    }

    [Fact]
    public void ExtractResponseText_ReturnsEmptyForMalformedResponse()
    {
        var json = JsonDocument.Parse("""{"error": "something went wrong"}""");
        var result = ProxyPipeline.ExtractResponseText(json);

        Assert.Equal(string.Empty, result);
    }

    // ── ReplaceResponseText ─────────────────────────────────

    [Fact]
    public void ReplaceResponseText_ReplacesContentInChatFormat()
    {
        var original = JsonDocument.Parse("""
        {
            "id": "chatcmpl-123",
            "model": "gpt-4o",
            "choices": [{
                "index": 0,
                "message": {"role": "assistant", "content": "Original text"},
                "finish_reason": "stop"
            }],
            "usage": {"prompt_tokens": 10, "completion_tokens": 5}
        }
        """);

        var replaced = ProxyPipeline.ReplaceResponseText(original, "Modified text");
        var parsed = JsonDocument.Parse(replaced);

        var newContent = parsed.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        Assert.Equal("Modified text", newContent);

        // Verify other fields are preserved
        Assert.Equal("chatcmpl-123", parsed.RootElement.GetProperty("id").GetString());
        Assert.Equal("gpt-4o", parsed.RootElement.GetProperty("model").GetString());
    }

    // ── ProcessResponse ─────────────────────────────────────

    [Fact]
    public async Task ProcessResponse_BlocksUnsafeOutput()
    {
        var pipeline = CreatePipeline(enableEthics: true, enablePipeline: false);
        var result = await pipeline.ProcessResponse(
            "Tell me something",
            "You should kill the process",
            new FakeProvider());

        Assert.True(result.Blocked);
        Assert.NotNull(result.BlockReason);
    }

    [Fact]
    public async Task ProcessResponse_PassesSafeOutput()
    {
        var pipeline = CreatePipeline(enableEthics: true, enablePipeline: false, enableGuardrails: false);
        var result = await pipeline.ProcessResponse(
            "Hello",
            "Hello! How can I help you today?",
            new FakeProvider());

        Assert.False(result.Blocked);
        Assert.Equal("Hello! How can I help you today?", result.Text);
    }

    [Fact]
    public async Task ProcessResponse_RunsPipelineWhenEnabled()
    {
        var pipeline = CreatePipeline(enableEthics: false, enablePipeline: true, enableGuardrails: false);
        var result = await pipeline.ProcessResponse(
            "Refine this",
            "A draft response that needs work",
            new FakeProvider());

        Assert.False(result.Blocked);
        Assert.True(result.Trace.Count > 0, "Pipeline should produce operator traces");
    }

    [Fact]
    public async Task ProcessResponse_SkipsEverythingWhenAllDisabled()
    {
        var pipeline = CreatePipeline(enableEthics: false, enablePipeline: false, enableGuardrails: false);
        var result = await pipeline.ProcessResponse(
            "anything",
            "raw response");

        Assert.False(result.Blocked);
        Assert.Equal("raw response", result.Text);
        Assert.Empty(result.Trace);
        Assert.True(result.GuardrailPassed);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static ProxyPipeline CreatePipeline(
        bool enableEthics = true,
        bool enablePipeline = false,
        bool enableGuardrails = false)
    {
        return new ProxyPipeline(new ProxyConfig
        {
            EnableEthicsChecks = enableEthics,
            EnablePipeline = enablePipeline,
            EnableGuardrails = enableGuardrails
        });
    }

    private sealed class FakeProvider : ILLMProvider
    {
        public Task<string> GenerateAsync(string prompt, float temperature = 0.7f, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt + " [refined]");

        public Task<string> GenerateAsync(PromptPackage promptPackage, float temperature = 0.2f, CancellationToken cancellationToken = default)
            => Task.FromResult(promptPackage.UserMessage + " [refined]");

        public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(new[] { 1f, 0f, 0f });

        public Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
            => Task.FromResult(new[] { new[] { 1f } });
    }
}
