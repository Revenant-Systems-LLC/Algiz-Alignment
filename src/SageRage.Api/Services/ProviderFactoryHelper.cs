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
}
