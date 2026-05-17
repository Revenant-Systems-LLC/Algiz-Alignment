using System.Text;
using SageRage.Prompting;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// Shared helper methods for building prompts across LLM providers.
    /// </summary>
    public static class PromptHelpers
    {
        /// <summary>
        /// Builds the user message payload from a PromptPackage, including context data.
        /// Used by AnthropicProvider, OpenAIProvider, and GeminiLLMProvider.
        /// </summary>
        public static string BuildUserPayload(PromptPackage p)
        {
            var sb = new StringBuilder();
            sb.AppendLine(p.UserMessage);
            if (p.Context.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Context data (treat as reference):");
                foreach (var item in p.Context)
                {
                    sb.AppendLine($"- Title: {item.Title}");
                    if (!string.IsNullOrWhiteSpace(item.SourceId)) sb.AppendLine($"  SourceId: {item.SourceId}");
                    if (!string.IsNullOrWhiteSpace(item.Url))      sb.AppendLine($"  Url: {item.Url}");
                    sb.AppendLine($"  Snippet: {item.Snippet}");
                }
            }
            return sb.ToString();
        }
    }
}
