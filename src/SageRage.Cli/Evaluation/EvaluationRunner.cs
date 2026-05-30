using System.Text.Json;
using SageRage.Domain;
using SageRage.Governance;
using SageRage.Infrastructure;

namespace SageRage.Cli.Evaluation;

public class EvaluationRunner
{
    public async Task RunAsync(string datasetPath, string outputPath)
    {
        Console.WriteLine($"Loading dataset from {datasetPath}...");
        var json = await File.ReadAllTextAsync(datasetPath);
        var dataset = JsonSerializer.Deserialize<EvaluationDataset>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? throw new InvalidOperationException("Failed to parse dataset.");

        Console.WriteLine($"Loaded dataset '{dataset.Name}' with {dataset.Prompts.Count} prompts.");
        
        // Load secrets
        var (success, loaded, config) = SecretLoader.LoadFromConfig();
        if (!success)
        {
            Console.WriteLine("Warning: Failed to load secrets from config. Make sure the secrets drive is unlocked and you've run setup.");
        }

        var providersToTest = new List<GovernanceProfile>
        {
            new GovernanceProfile
            {
                DisplayName = "Gemini",
                ProviderType = "Gemini",
                Model = "gemini-2.0-flash",
                ApiKeyRef = SecretLoader.GetKey("GEMINI_API_KEY") ?? "not-found",
                Tags = { "benchmark" }
            },
            new GovernanceProfile
            {
                DisplayName = "Local",
                ProviderType = "OpenAI-Compatible",
                Model = "local-model",
                BaseUrl = "http://localhost:1234/v1",
                ApiKeyRef = "not-needed",
                Tags = { "benchmark" }
            },
            new GovernanceProfile
            {
                DisplayName = "GPT",
                ProviderType = "OpenAI",
                Model = "gpt-4o",
                ApiKeyRef = SecretLoader.GetKey("OPENAI_API_KEY") ?? "not-found",
                Tags = { "benchmark" }
            }
        };

        var report = new EvaluationReport();

        foreach (var prompt in dataset.Prompts)
        {
            Console.WriteLine($"\n--- Testing Prompt: {prompt.Id} ({prompt.Category}) ---");
            Console.WriteLine($"Text: {prompt.Text}");

            foreach (var profile in providersToTest)
            {
                Console.WriteLine($"\nProvider: {profile.DisplayName}");
                
                try 
                {
                    ILLMProvider llm = CreateProvider(profile);
                    
                    // 1. Raw Output (Without SAIGE-RAGE)
                    Console.WriteLine("  Generating Raw Output...");
                    string rawOutput = "Failed";
                    try
                    {
                        rawOutput = await llm.GenerateAsync(prompt.Text, 0.7f);
                    }
                    catch (Exception ex)
                    {
                        rawOutput = $"[Error: {ex.Message}]";
                    }
                    Console.WriteLine($"  Raw: {rawOutput.Replace("\n", " ")}");

                    // 2. Governed Output (With SAIGE-RAGE)
                    Console.WriteLine("  Generating Governed Output...");
                    var engine = new GovernedEngine(profile, llm);
                    engine.Start();
                    
                    var userMessage = new UserMessage { Text = prompt.Text };
                    var governedResponse = await engine.ProcessAsync(userMessage);
                    var snapshot = engine.TakeSnapshot();
                    
                    Console.WriteLine($"  Governed: {governedResponse.Text.Replace("\n", " ")}");
                    Console.WriteLine($"  QC Passed: {snapshot.LastRequestClean}, Malice: {snapshot.Malice:F2}, Coherence: {snapshot.Coherence:F2}");

                    report.Results.Add(new EvaluationResult
                    {
                        PromptId = prompt.Id,
                        Category = prompt.Category,
                        Provider = profile.DisplayName,
                        RawOutput = rawOutput,
                        GovernedOutput = governedResponse.Text,
                        QcPassed = snapshot.LastRequestClean,
                        MaliceScore = snapshot.Malice,
                        Coherence = snapshot.Coherence,
                        Entropy = snapshot.Entropy,
                        Blocked = snapshot.BlockedRequests > 0 || snapshot.Status == GovernanceStatus.Blocked,
                        TraceOps = snapshot.LastTraceOperators?.ToArray()
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Failed to test provider {profile.DisplayName}: {ex.Message}");
                }
            }
        }

        Console.WriteLine($"\nWriting results to {outputPath}...");
        var outJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, outJson);
        Console.WriteLine("Evaluation complete.");
    }

    private ILLMProvider CreateProvider(GovernanceProfile profile)
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

            _ => throw new NotSupportedException($"Provider type '{profile.ProviderType}' is not supported.")
        };
    }
}
