using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Domain;
using SageRage.Guardrails;
using SageRage.Infrastructure;
using SageRage.Prompting;

namespace SageRage.Proxy
{
    /// <summary>
    /// Result of running a message through the SAGE-RAGE pipeline.
    /// </summary>
    public sealed class PipelineResult
    {
        public required string Text { get; init; }
        public bool Blocked { get; init; }
        public string? BlockReason { get; init; }
        public bool GuardrailPassed { get; init; } = true;
        public IReadOnlyList<string> Findings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<OperatorTrace> Trace { get; init; } = Array.Empty<OperatorTrace>();
        public float? Coherence { get; init; }
        public float? Entropy { get; init; }
    }

    /// <summary>
    /// Runs intercepted LLM request/response content through the SAGE-RAGE
    /// ethics stack, operator pipeline, and guardrail checks.
    /// Stateless per-request — no conversation memory across proxy calls.
    /// </summary>
    public sealed class ProxyPipeline
    {
        private readonly RageEthicsStack _ethics = new();
        private readonly ProxyConfig _config;
        private readonly RageEngine _engine;

        public ProxyPipeline(ProxyConfig config, ILLMProvider llm)
        {
            _config = config;
            _engine = new RageEngine(llm);
        }

        /// <summary>
        /// Check user input against ethics stack (Layer 0+).
        /// Returns null if cleared, or a block reason string if rejected.
        /// </summary>
        public string? CheckInput(string userText)
        {
            if (!_config.EnableEthicsChecks)
                return null;

            // Normalize/de-obfuscate before matching, so a zero-width-space or
            // letter-spaced payload can't slip past the proxy's input check.
            var sanitized = _ethics.SanitizeForProcessing(userText);
            var clearance = _ethics.EvaluateInput(sanitized);
            return clearance.Allowed ? null : clearance.Reason;
        }

        /// <summary>
        /// Run the full pipeline on an LLM response: ethics check, operators, guardrails.
        /// </summary>
        public async Task<PipelineResult> ProcessResponse(
            string userText,
            string responseText,
            CancellationToken cancellationToken = default)
        {
            // 1. Ethics check on output
            if (_config.EnableEthicsChecks)
            {
                var clearance = _ethics.EvaluateOutput(responseText);
                if (!clearance.Allowed)
                {
                    return new PipelineResult
                    {
                        Text = responseText,
                        Blocked = true,
                        BlockReason = clearance.Reason
                    };
                }
            }

            // 2. Run operator pipeline if enabled
            var trace = Array.Empty<OperatorTrace>();
            float? coherence = null;
            float? entropy = null;
            var processedText = responseText;

            if (_config.EnablePipeline)
            {
                var state = new SageState { Text = responseText };

                var transformed = await _engine.ExecuteSequence(
                    _config.Operators, state, cancellationToken);

                processedText = transformed.Text;
                trace = transformed.Trace.Steps.ToArray();
                coherence = transformed.Coherence;
                entropy = transformed.Entropy;
            }

            // 3. Guardrail checks if enabled
            var guardrailPassed = true;
            var findings = Array.Empty<string>();

            if (_config.EnableGuardrails)
            {
                var qcContext = BuildQcContext(userText);
                var profile = SageProfileSelector.Select(userText, qcContext);
                var qcResult = await SageGuardrailController.EvaluateAsync(
                    userText, processedText, Array.Empty<ContextItem>(), profile, qcContext);

                guardrailPassed = qcResult.Passed;
                findings = qcResult.Findings.ToArray();
            }

            return new PipelineResult
            {
                Text = processedText,
                Blocked = false,
                GuardrailPassed = guardrailPassed,
                Findings = findings,
                Trace = trace,
                Coherence = coherence,
                Entropy = entropy
            };
        }

        /// <summary>
        /// Extract the last user message text from an OpenAI-format messages array.
        /// </summary>
        public static string ExtractUserText(JsonElement messagesElement)
        {
            if (messagesElement.ValueKind != JsonValueKind.Array)
                return string.Empty;

            // Walk backwards to find the last user message
            var length = messagesElement.GetArrayLength();
            for (var i = length - 1; i >= 0; i--)
            {
                var msg = messagesElement[i];
                if (msg.TryGetProperty("role", out var role) &&
                    role.GetString() == "user" &&
                    msg.TryGetProperty("content", out var content))
                {
                    return content.ValueKind == JsonValueKind.String
                        ? content.GetString() ?? string.Empty
                        : content.ToString();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Extract the assistant response text from an OpenAI-format completion response.
        /// </summary>
        public static string ExtractResponseText(JsonDocument responseDoc)
        {
            var root = responseDoc.RootElement;

            // Chat completions format: choices[0].message.content
            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }

                // Legacy completions format: choices[0].text
                if (first.TryGetProperty("text", out var text))
                    return text.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Replace the assistant response text in an OpenAI-format completion response.
        /// Returns the modified JSON as a byte array.
        /// </summary>
        public static byte[] ReplaceResponseText(JsonDocument original, string newText)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            var root = original.RootElement;
            WriteElementWithReplacement(writer, root, newText);

            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteElementWithReplacement(
            Utf8JsonWriter writer, JsonElement element, string newText,
            bool insideChoice = false, bool insideMessage = false)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var prop in element.EnumerateObject())
                    {
                        writer.WritePropertyName(prop.Name);

                        if (prop.Name == "choices")
                        {
                            WriteElementWithReplacement(writer, prop.Value, newText, insideChoice: true);
                        }
                        else if (insideChoice && prop.Name == "message")
                        {
                            WriteElementWithReplacement(writer, prop.Value, newText, insideMessage: true);
                        }
                        else if (insideMessage && prop.Name == "content")
                        {
                            writer.WriteStringValue(newText);
                        }
                        else if (insideChoice && prop.Name == "text")
                        {
                            // Legacy completions format
                            writer.WriteStringValue(newText);
                        }
                        else
                        {
                            WriteElementWithReplacement(writer, prop.Value, newText, insideChoice, insideMessage);
                        }
                    }
                    writer.WriteEndObject();
                    break;

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    var firstInArray = true;
                    foreach (var item in element.EnumerateArray())
                    {
                        // Only replace text in the first choice
                        if (insideChoice && firstInArray)
                        {
                            WriteElementWithReplacement(writer, item, newText, insideChoice: true);
                            firstInArray = false;
                        }
                        else
                        {
                            WriteElementWithReplacement(writer, item, newText, insideChoice: false);
                        }
                    }
                    writer.WriteEndArray();
                    break;

                default:
                    element.WriteTo(writer);
                    break;
            }
        }

        private static QCContext BuildQcContext(string userText)
            => new()
            {
                TaskKind = DetectTaskKind(userText),
                HasAnchorsPi = false,
                TensionMagnitude = 0.2,
                UserRequestedSources = userText.Contains("source", StringComparison.OrdinalIgnoreCase)
                                       || userText.Contains("citation", StringComparison.OrdinalIgnoreCase),
                UserRequestedRecency = userText.Contains("latest", StringComparison.OrdinalIgnoreCase)
                                       || userText.Contains("current", StringComparison.OrdinalIgnoreCase)
            };

        private static TaskKind DetectTaskKind(string text)
        {
            if (text.Contains("legal", StringComparison.OrdinalIgnoreCase)
                || text.Contains("medical", StringComparison.OrdinalIgnoreCase)
                || text.Contains("financial", StringComparison.OrdinalIgnoreCase))
                return TaskKind.HighStakes;

            if (text.ContainsAny("latest", "current", "today", "recent", "newest"))
                return TaskKind.RecencySensitive;

            if (text.ContainsAny("how many", "percent", "stats", "date", "when"))
                return TaskKind.Factual;

            return TaskKind.Casual;
        }
    }
}
