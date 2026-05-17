using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SageRage.Domain;
using SageRage.Infrastructure;

namespace SageRage.Proxy
{
    /// <summary>
    /// SAGE-RAGE alignment proxy server.
    /// Sits between any application and any OpenAI-compatible LLM API.
    /// Intercepts requests/responses and runs them through the pipeline.
    /// </summary>
    public sealed class ProxyServer : IAsyncDisposable
    {
        private readonly ProxyConfig _config;
        private readonly ProxyPipeline _pipeline;
        private readonly HttpClient _upstream;
        private readonly ILLMProvider _pipelineLlm;
        private WebApplication? _app;
        private int _requestCount;

        public ProxyServer(ProxyConfig config)
        {
            _config = config;
            _upstream = new HttpClient { Timeout = config.UpstreamTimeout };

            // The pipeline LLM is used by operators (Omega, Chi, Sigma) for
            // refinement calls. It talks to the same upstream the proxy forwards to.
            _pipelineLlm = new OpenAICompatibleProvider(
                baseUrl: config.UpstreamBaseUrl,
                model: "proxy-passthrough",
                apiKey: config.UpstreamApiKey ?? "not-needed");

            _pipeline = new ProxyPipeline(config, _pipelineLlm);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var builder = WebApplication.CreateSlimBuilder();

            builder.WebHost.UseUrls(_config.ListenUrl);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            _app = builder.Build();

            // Health check
            _app.MapGet("/health", () => Results.Ok(new
            {
                status = "healthy",
                service = "sage-rage-proxy",
                requests = _requestCount,
                config = new
                {
                    upstream = _config.UpstreamBaseUrl,
                    pipeline = _config.EnablePipeline,
                    ethics = _config.EnableEthicsChecks,
                    guardrails = _config.EnableGuardrails,
                    operators = string.Join(",", _config.Operators)
                }
            }));

            // Main proxy endpoints
            _app.MapPost("/v1/chat/completions", HandleChatCompletions);
            _app.MapPost("/v1/completions", HandleChatCompletions);  // Same handler

            // Pass-through for everything else under /v1/
            _app.Map("/v1/{**rest}", HandlePassThrough);

            await _app.StartAsync(cancellationToken);
        }

        public async Task StopAsync()
        {
            if (_app is not null)
                await _app.StopAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _upstream.Dispose();
        }

        // ── Chat Completions Handler ─────────────────────────────────

        private async Task HandleChatCompletions(HttpContext ctx)
        {
            var requestId = Interlocked.Increment(ref _requestCount);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Read the request body
                using var bodyStream = new MemoryStream();
                await ctx.Request.Body.CopyToAsync(bodyStream, ctx.RequestAborted);
                var bodyBytes = bodyStream.ToArray();

                using var requestDoc = JsonDocument.Parse(bodyBytes);
                var root = requestDoc.RootElement;

                // Extract user message for ethics checks
                var userText = string.Empty;
                if (root.TryGetProperty("messages", out var messages))
                    userText = ProxyPipeline.ExtractUserText(messages);

                // 1. Input ethics check
                var blockReason = _pipeline.CheckInput(userText);
                if (blockReason is not null)
                {
                    await WriteBlockedResponse(ctx, blockReason, requestId);
                    LogRequest(requestId, "BLOCKED_INPUT", userText, blockReason, sw.Elapsed);
                    return;
                }

                // Check if this is a streaming request
                var isStreaming = root.TryGetProperty("stream", out var streamProp) &&
                                  streamProp.ValueKind == JsonValueKind.True;

                if (isStreaming && !_config.ForceNonStreamForPipeline)
                {
                    await HandleStreamingRequest(ctx, bodyBytes, userText, requestId, sw);
                }
                else
                {
                    // Force non-streaming even if client asked for streaming
                    var forwardBytes = bodyBytes;
                    if (isStreaming && _config.ForceNonStreamForPipeline)
                        forwardBytes = SetStreamFalse(bodyBytes);

                    await HandleNonStreamingRequest(ctx, forwardBytes, userText, requestId, sw);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 502;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    error = new { message = $"Proxy error: {ex.Message}", type = "proxy_error" }
                });
                LogRequest(requestId, "ERROR", "", ex.Message, sw.Elapsed);
            }
        }

        private async Task HandleNonStreamingRequest(
            HttpContext ctx, byte[] bodyBytes, string userText,
            int requestId, System.Diagnostics.Stopwatch sw)
        {
            // Forward to upstream
            using var upstreamRequest = BuildUpstreamRequest(
                ctx, "/v1/chat/completions", bodyBytes);
            using var upstreamResponse = await _upstream.SendAsync(
                upstreamRequest, ctx.RequestAborted);

            var responseBody = await upstreamResponse.Content.ReadAsByteArrayAsync(
                ctx.RequestAborted);

            // If upstream returned an error, pass it through
            if (!upstreamResponse.IsSuccessStatusCode)
            {
                ctx.Response.StatusCode = (int)upstreamResponse.StatusCode;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Body.WriteAsync(responseBody, ctx.RequestAborted);
                LogRequest(requestId, "UPSTREAM_ERROR",
                    userText, $"HTTP {(int)upstreamResponse.StatusCode}", sw.Elapsed);
                return;
            }

            // Parse the response and extract text
            using var responseDoc = JsonDocument.Parse(responseBody);
            var responseText = ProxyPipeline.ExtractResponseText(responseDoc);

            // Run through SAGE-RAGE pipeline
            var result = await _pipeline.ProcessResponse(
                userText, responseText, ctx.RequestAborted);

            if (result.Blocked)
            {
                await WriteBlockedResponse(ctx, result.BlockReason!, requestId);
                LogRequest(requestId, "BLOCKED_OUTPUT", userText,
                    result.BlockReason!, sw.Elapsed);
                return;
            }

            // Replace response text if pipeline modified it
            byte[] finalBody;
            if (result.Text != responseText)
                finalBody = ProxyPipeline.ReplaceResponseText(responseDoc, result.Text);
            else
                finalBody = responseBody;

            // Write response with trace headers
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";

            if (_config.IncludeTraceHeaders)
            {
                ctx.Response.Headers["X-SageRage-Pipeline"] = _config.EnablePipeline ? "active" : "bypass";
                ctx.Response.Headers["X-SageRage-Guardrail"] = result.GuardrailPassed ? "passed" : "flagged";
                ctx.Response.Headers["X-SageRage-RequestId"] = requestId.ToString();

                if (result.Coherence.HasValue)
                    ctx.Response.Headers["X-SageRage-Coherence"] = result.Coherence.Value.ToString("F3");
                if (result.Entropy.HasValue)
                    ctx.Response.Headers["X-SageRage-Entropy"] = result.Entropy.Value.ToString("F3");
                if (result.Trace.Count > 0)
                    ctx.Response.Headers["X-SageRage-Operators"] =
                        string.Join(",", result.Trace.Select(t => t.Operator));
            }

            await ctx.Response.Body.WriteAsync(finalBody, ctx.RequestAborted);
            LogRequest(requestId, "OK", userText,
                $"pipeline={_config.EnablePipeline} guardrail={result.GuardrailPassed}", sw.Elapsed);
        }

        private async Task HandleStreamingRequest(
            HttpContext ctx, byte[] bodyBytes, string userText,
            int requestId, System.Diagnostics.Stopwatch sw)
        {
            // Forward to upstream as streaming
            using var upstreamRequest = BuildUpstreamRequest(
                ctx, "/v1/chat/completions", bodyBytes);
            using var upstreamResponse = await _upstream.SendAsync(
                upstreamRequest, HttpCompletionOption.ResponseHeadersRead,
                ctx.RequestAborted);

            if (!upstreamResponse.IsSuccessStatusCode)
            {
                ctx.Response.StatusCode = (int)upstreamResponse.StatusCode;
                ctx.Response.ContentType = "application/json";
                var errorBody = await upstreamResponse.Content.ReadAsByteArrayAsync(
                    ctx.RequestAborted);
                await ctx.Response.Body.WriteAsync(errorBody, ctx.RequestAborted);
                return;
            }

            // Set up SSE response
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.Headers["Connection"] = "keep-alive";

            if (_config.IncludeTraceHeaders)
            {
                ctx.Response.Headers["X-SageRage-Pipeline"] = "stream-passthrough";
                ctx.Response.Headers["X-SageRage-RequestId"] = requestId.ToString();
            }

            // Pass through the stream while capturing text for logging
            var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(
                ctx.RequestAborted);
            var capturedText = await StreamRewriter.PassThroughAndCapture(
                upstreamStream, ctx.Response.Body, ctx.RequestAborted);

            LogRequest(requestId, "STREAM_OK", userText,
                $"chars={capturedText.Length}", sw.Elapsed);
        }

        // ── Pass-Through Handler ─────────────────────────────────────

        private async Task HandlePassThrough(HttpContext ctx)
        {
            var path = ctx.Request.Path.Value ?? "/";
            var query = ctx.Request.QueryString.Value ?? "";

            using var bodyStream = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(bodyStream, ctx.RequestAborted);

            using var upstreamRequest = BuildUpstreamRequest(
                ctx, path + query, bodyStream.ToArray());
            upstreamRequest.Method = new HttpMethod(ctx.Request.Method);

            try
            {
                using var upstreamResponse = await _upstream.SendAsync(
                    upstreamRequest, ctx.RequestAborted);

                ctx.Response.StatusCode = (int)upstreamResponse.StatusCode;
                foreach (var header in upstreamResponse.Content.Headers)
                    ctx.Response.Headers[header.Key] = header.Value.ToArray();

                var body = await upstreamResponse.Content.ReadAsByteArrayAsync(
                    ctx.RequestAborted);
                await ctx.Response.Body.WriteAsync(body, ctx.RequestAborted);
            }
            catch (HttpRequestException ex)
            {
                ctx.Response.StatusCode = 502;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    error = new { message = $"Upstream unreachable: {ex.Message}", type = "proxy_error" }
                });
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private HttpRequestMessage BuildUpstreamRequest(
            HttpContext ctx, string path, byte[] body)
        {
            var targetUrl = _config.UpstreamBaseUrl.TrimEnd('/') + path;
            var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
            {
                Content = new ByteArrayContent(body)
            };

            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            // Forward auth header from client, or use configured key
            if (!string.IsNullOrWhiteSpace(_config.UpstreamApiKey))
            {
                request.Headers.TryAddWithoutValidation(
                    "Authorization", $"Bearer {_config.UpstreamApiKey}");
            }
            else if (ctx.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            // Forward Anthropic-specific headers if present
            if (ctx.Request.Headers.TryGetValue("x-api-key", out var apiKey))
                request.Headers.TryAddWithoutValidation("x-api-key", apiKey.ToString());
            if (ctx.Request.Headers.TryGetValue("anthropic-version", out var anthropicVersion))
                request.Headers.TryAddWithoutValidation("anthropic-version", anthropicVersion.ToString());

            return request;
        }

        private static async Task WriteBlockedResponse(
            HttpContext ctx, string reason, int requestId)
        {
            ctx.Response.StatusCode = 200; // Return 200 with a refusal, not an HTTP error
            ctx.Response.ContentType = "application/json";

            var blocked = new
            {
                id = $"chatcmpl-blocked-{requestId}",
                @object = "chat.completion",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = "sage-rage-proxy",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new
                        {
                            role = "assistant",
                            content = $"[SAGE-RAGE] Request blocked by ethics layer: {reason}"
                        },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
            };

            await ctx.Response.WriteAsJsonAsync(blocked);
        }

        private static byte[] SetStreamFalse(byte[] originalBody)
        {
            using var doc = JsonDocument.Parse(originalBody);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "stream")
                {
                    writer.WriteBoolean("stream", false);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
            writer.Flush();

            return stream.ToArray();
        }

        private void LogRequest(
            int requestId, string status, string userText, string detail, TimeSpan elapsed)
        {
            var truncatedInput = userText.Length > 80
                ? userText[..80] + "..."
                : userText;

            var logLine = $"[{DateTime.UtcNow:HH:mm:ss.fff}] #{requestId} {status} " +
                          $"({elapsed.TotalMilliseconds:F0}ms) [{detail}] \"{truncatedInput}\"";

            Console.WriteLine(logLine);

            if (_config.LogDirectory is null) return;

            try
            {
                Directory.CreateDirectory(_config.LogDirectory);
                var logFile = Path.Combine(_config.LogDirectory,
                    $"proxy-{DateTime.UtcNow:yyyy-MM-dd}.log");
                File.AppendAllText(logFile, logLine + Environment.NewLine);
            }
            catch
            {
                // Don't crash the proxy over logging failures
            }
        }
    }
}
