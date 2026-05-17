using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SageRage.Proxy;
using Xunit;

namespace SageRage.Proxy.Tests;

public class StreamRewriterTests
{
    [Fact]
    public async Task BufferStream_ReconstructsTextFromSSEChunks()
    {
        var sse = BuildSSEStream(
            """{"choices":[{"delta":{"content":"Hello"}}]}""",
            """{"choices":[{"delta":{"content":" world"}}]}""",
            """{"choices":[{"delta":{"content":"!"}}]}""",
            "[DONE]");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        var (fullText, rawLines) = await StreamRewriter.BufferStream(stream);

        Assert.Equal("Hello world!", fullText);
        Assert.True(rawLines.Count >= 4, "Should capture all SSE lines");
    }

    [Fact]
    public async Task BufferStream_HandlesEmptyStream()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());
        var (fullText, rawLines) = await StreamRewriter.BufferStream(stream);

        Assert.Equal(string.Empty, fullText);
        Assert.Empty(rawLines);
    }

    [Fact]
    public async Task BufferStream_SkipsMalformedChunks()
    {
        var sse = BuildSSEStream(
            """{"choices":[{"delta":{"content":"Good"}}]}""",
            "not valid json at all",
            """{"choices":[{"delta":{"content":" stuff"}}]}""",
            "[DONE]");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        var (fullText, _) = await StreamRewriter.BufferStream(stream);

        Assert.Equal("Good stuff", fullText);
    }

    [Fact]
    public async Task BufferStream_IgnoresChunksWithoutDelta()
    {
        var sse = BuildSSEStream(
            """{"choices":[{"delta":{"role":"assistant"}}]}""",
            """{"choices":[{"delta":{"content":"Text"}}]}""",
            "[DONE]");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        var (fullText, _) = await StreamRewriter.BufferStream(stream);

        Assert.Equal("Text", fullText);
    }

    [Fact]
    public void RebuildAsNonStreaming_CreatesValidResponse()
    {
        var rawLines = new System.Collections.Generic.List<string>
        {
            """data: {"id":"chatcmpl-abc","model":"gpt-4o","created":1700000000,"choices":[{"delta":{"content":"Hi"}}]}""",
            "",
            """data: {"id":"chatcmpl-abc","model":"gpt-4o","created":1700000000,"choices":[{"delta":{"content":"!"}}]}""",
            "",
            "data: [DONE]",
            ""
        };

        var result = StreamRewriter.RebuildAsNonStreaming(rawLines, "Processed text");
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.Equal("chatcmpl-abc", root.GetProperty("id").GetString());
        Assert.Equal("gpt-4o", root.GetProperty("model").GetString());
        Assert.Equal(1700000000, root.GetProperty("created").GetInt64());

        var content = root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        Assert.Equal("Processed text", content);

        var finishReason = root.GetProperty("choices")[0]
            .GetProperty("finish_reason")
            .GetString();
        Assert.Equal("stop", finishReason);
    }

    [Fact]
    public void RebuildAsNonStreaming_HandlesMissingMetadata()
    {
        var rawLines = new System.Collections.Generic.List<string>
        {
            "data: [DONE]"
        };

        var result = StreamRewriter.RebuildAsNonStreaming(rawLines, "Fallback text");
        var doc = JsonDocument.Parse(result);

        var content = doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        Assert.Equal("Fallback text", content);

        // Should still have default id and model
        Assert.Equal("chatcmpl-proxy", doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task PassThroughAndCapture_WritesToClientAndCapturesText()
    {
        var sse = BuildSSEStream(
            """{"choices":[{"delta":{"content":"Streamed"}}]}""",
            """{"choices":[{"delta":{"content":" content"}}]}""",
            "[DONE]");

        using var upstream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        using var client = new MemoryStream();

        var captured = await StreamRewriter.PassThroughAndCapture(upstream, client);

        Assert.Equal("Streamed content", captured);
        Assert.True(client.Length > 0, "Client stream should have received data");

        // Verify client received the SSE lines
        client.Position = 0;
        var clientText = Encoding.UTF8.GetString(client.ToArray());
        Assert.Contains("data:", clientText);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static string BuildSSEStream(params string[] dataPayloads)
    {
        var sb = new StringBuilder();
        foreach (var payload in dataPayloads)
        {
            sb.AppendLine($"data: {payload}");
            sb.AppendLine(); // Blank line between events
        }
        return sb.ToString();
    }
}
