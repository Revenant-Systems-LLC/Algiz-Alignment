using System;
using System.Collections.Generic;

namespace SageRage.Proxy
{
    /// <summary>
    /// Configuration for the SAGE-RAGE proxy server.
    /// </summary>
    public sealed class ProxyConfig
    {
        /// <summary>Upstream LLM API base URL (e.g. https://api.openai.com/v1).</summary>
        public string UpstreamBaseUrl { get; init; } = "https://api.openai.com/v1";

        /// <summary>Port the proxy listens on.</summary>
        public int Port { get; init; } = 9443;

        /// <summary>Bind address (default: localhost only).</summary>
        public string BindAddress { get; init; } = "localhost";

        /// <summary>
        /// Operators to run on non-streaming responses.
        /// Default: Containment + Omega + Chi (same as Casual pipeline).
        /// </summary>
        public IReadOnlyList<CoreOperator> Operators { get; init; } =
            new[] { CoreOperator.Containment, CoreOperator.Omega, CoreOperator.Chi };

        /// <summary>Run the full SAGE-RAGE pipeline (operators + guardrails) on responses.</summary>
        public bool EnablePipeline { get; init; } = true;

        /// <summary>Run ethics checks on input and output.</summary>
        public bool EnableEthicsChecks { get; init; } = true;

        /// <summary>Run guardrail quality checks on output.</summary>
        public bool EnableGuardrails { get; init; } = true;

        /// <summary>
        /// For streaming requests: if true, buffer all chunks, run full pipeline,
        /// then return as non-streaming. If false, do input ethics check only
        /// and pass SSE stream through.
        /// </summary>
        public bool ForceNonStreamForPipeline { get; init; } = false;

        /// <summary>Log directory for request/response audit trails. Null = no logging.</summary>
        public string? LogDirectory { get; init; }

        /// <summary>Add SAGE-RAGE trace headers to proxied responses.</summary>
        public bool IncludeTraceHeaders { get; init; } = true;

        /// <summary>Request timeout for upstream calls.</summary>
        public TimeSpan UpstreamTimeout { get; init; } = TimeSpan.FromSeconds(120);

        /// <summary>Optional API key to forward to upstream (overrides client-sent key).</summary>
        public string? UpstreamApiKey { get; init; }

        /// <summary>Optional persona system instruction override.</summary>
        public string? PersonaInstruction { get; init; }

        public string ListenUrl => $"http://{BindAddress}:{Port}";
    }
}
