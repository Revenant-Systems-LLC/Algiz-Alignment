using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SageRage.Proxy
{
    /// <summary>
    /// Handles SSE (Server-Sent Events) streams from upstream LLM APIs.
    /// Can either pass through directly or buffer for full pipeline processing.
    /// </summary>
    public static class StreamRewriter
    {
        /// <summary>
        /// Buffer all SSE chunks from a streaming response, reconstruct the
        /// complete response text, and return both the text and the raw chunks.
        /// </summary>
        public static async Task<(string fullText, List<string> rawLines)> BufferStream(
            Stream responseStream,
            CancellationToken cancellationToken = default)
        {
            var textBuilder = new StringBuilder();
            var rawLines = new List<string>();

            using var reader = new StreamReader(responseStream, Encoding.UTF8);

            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;

                rawLines.Add(line);

                if (!line.StartsWith("data: ", StringComparison.Ordinal))
                    continue;

                var data = line["data: ".Length..];
                if (data == "[DONE]")
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var delta = ExtractDeltaContent(doc.RootElement);
                    if (delta is not null)
                        textBuilder.Append(delta);
                }
                catch (JsonException)
                {
                    // Malformed chunk — skip
                }
            }

            return (textBuilder.ToString(), rawLines);
        }

        /// <summary>
        /// Pass through an SSE stream from upstream to the client response,
        /// while also capturing the full text for post-hoc analysis.
        /// </summary>
        public static async Task<string> PassThroughAndCapture(
            Stream upstreamBody,
            Stream clientBody,
            CancellationToken cancellationToken = default)
        {
            var textBuilder = new StringBuilder();
            using var reader = new StreamReader(upstreamBody, Encoding.UTF8);

            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;

                // Forward the line to the client immediately
                var lineBytes = Encoding.UTF8.GetBytes(line + "\n");
                await clientBody.WriteAsync(lineBytes, cancellationToken);
                await clientBody.FlushAsync(cancellationToken);

                // Also capture content
                if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    var data = line["data: ".Length..];
                    if (data != "[DONE]")
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(data);
                            var delta = ExtractDeltaContent(doc.RootElement);
                            if (delta is not null)
                                textBuilder.Append(delta);
                        }
                        catch (JsonException) { }
                    }
                }
            }

            return textBuilder.ToString();
        }

        /// <summary>
        /// Rebuild a non-streaming response JSON from buffered streaming chunks.
        /// Takes the original first chunk (for id, model, etc.) and the full text.
        /// </summary>
        public static byte[] RebuildAsNonStreaming(
            List<string> rawLines, string processedText)
        {
            // Find the first data line to get metadata (id, model, created)
            string? firstDataLine = null;
            foreach (var line in rawLines)
            {
                if (line.StartsWith("data: ", StringComparison.Ordinal) &&
                    !line.EndsWith("[DONE]", StringComparison.Ordinal))
                {
                    firstDataLine = line["data: ".Length..];
                    break;
                }
            }

            string id = "chatcmpl-proxy";
            string model = "unknown";
            long created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (firstDataLine is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(firstDataLine);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var idProp))
                        id = idProp.GetString() ?? id;
                    if (root.TryGetProperty("model", out var modelProp))
                        model = modelProp.GetString() ?? model;
                    if (root.TryGetProperty("created", out var createdProp))
                        created = createdProp.GetInt64();
                }
                catch (JsonException) { }
            }

            // Build a standard non-streaming response
            var response = new
            {
                id,
                @object = "chat.completion",
                created,
                model,
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = processedText },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
            };

            return JsonSerializer.SerializeToUtf8Bytes(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }

        /// <summary>
        /// Extract the content delta from a streaming chunk.
        /// Handles OpenAI format: choices[0].delta.content
        /// </summary>
        private static string? ExtractDeltaContent(JsonElement root)
        {
            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString();
                }
            }
            return null;
        }
    }
}
