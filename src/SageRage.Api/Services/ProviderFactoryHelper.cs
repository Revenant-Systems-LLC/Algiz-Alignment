using SageRage.Governance;
using SageRage.Infrastructure;

namespace SageRage.Api.Services;

/// <summary>
/// Resolves an ILLMProvider from a GovernanceProfile at runtime.
/// </summary>
public static class ProviderFactoryHelper
{
    public static ILLMProvider Create(GovernanceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var primary = CreatePrimary(profile);
        return WrapWithEmbeddings(primary, profile);
    }

    private static ILLMProvider CreatePrimary(GovernanceProfile profile)
    {
        return profile.ProviderType.ToUpperInvariant() switch
        {
            "GEMINI" or "GOOGLE" => new GeminiLLMProvider(
                geminiLiveApiKey: profile.ApiKeyRef,
                model: profile.Model),

            "ANTHROPIC" or "CLAUDE" => new AnthropicProvider(
                apiKey: profile.ApiKeyRef,
                model: profile.Model),

            "OPENAI" => new OpenAIProvider(
                apiKey: profile.ApiKeyRef,
                model: profile.Model),

            "OLLAMA" => new OllamaProvider(
                model: profile.Model,
                baseUrl: profile.BaseUrl ?? "http://localhost:11434"),

            "OPENAI-COMPATIBLE" or "LMSTUDIO" or "VLLM" or "LOCAL" => new OpenAICompatibleProvider(
                baseUrl: profile.BaseUrl ?? "http://localhost:1234/v1",
                model: profile.Model,
                apiKey: profile.ApiKeyRef ?? "not-needed"),

            _ => throw new NotSupportedException(
                $"Provider type '{profile.ProviderType}' is not supported. " +
                $"Supported types: Gemini, Anthropic, OpenAI, Ollama, OpenAI-Compatible.")
        };
    }

    /// <summary>
    /// Routes embeddings to a dedicated provider when the profile configures one
    /// (or the SAGE_EMBEDDINGS_PROVIDER environment variable is set). Anthropic and
    /// Gemini have no embeddings endpoint; without this, similarity-based operators
    /// run on lexical fallback heuristics only.
    /// </summary>
    private static ILLMProvider WrapWithEmbeddings(ILLMProvider primary, GovernanceProfile profile)
    {
        var type = profile.EmbeddingsProviderType
                   ?? Environment.GetEnvironmentVariable("SAGE_EMBEDDINGS_PROVIDER");
        if (string.IsNullOrWhiteSpace(type))
            return primary;

        var model = profile.EmbeddingsModel
                    ?? Environment.GetEnvironmentVariable("SAGE_EMBEDDINGS_MODEL");
        var baseUrl = profile.EmbeddingsBaseUrl
                      ?? Environment.GetEnvironmentVariable("SAGE_EMBEDDINGS_BASEURL");

        ILLMProvider embeddings = type.ToUpperInvariant() switch
        {
            "OLLAMA" => new OllamaProvider(
                model: model ?? "nomic-embed-text",
                baseUrl: baseUrl ?? "http://localhost:11434"),

            "OPENAI" => new OpenAIProvider(
                apiKey: SecretLoader.GetKey("OPENAI_API_KEY")),

            "OPENAI-COMPATIBLE" or "LMSTUDIO" or "VLLM" or "LOCAL" => new OpenAICompatibleProvider(
                baseUrl: baseUrl ?? "http://localhost:1234/v1",
                model: model ?? "text-embedding-nomic-embed-text-v1.5",
                apiKey: "not-needed"),

            _ => throw new NotSupportedException(
                $"Embeddings provider type '{type}' is not supported. " +
                $"Supported types: Ollama, OpenAI, OpenAI-Compatible.")
        };

        return new EmbeddingRoutingProvider(primary, embeddings);
    }
}
