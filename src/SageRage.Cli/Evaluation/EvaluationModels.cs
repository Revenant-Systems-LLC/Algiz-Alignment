using System.Text.Json.Serialization;

namespace SageRage.Cli.Evaluation;

public class EvaluationPrompt
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("text")]
    public required string Text { get; set; }

    [JsonPropertyName("category")]
    public required string Category { get; set; }
}

public class EvaluationDataset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("prompts")]
    public List<EvaluationPrompt> Prompts { get; set; } = new();
}

public class EvaluationResult
{
    [JsonPropertyName("promptId")]
    public required string PromptId { get; set; }

    [JsonPropertyName("category")]
    public required string Category { get; set; }

    [JsonPropertyName("provider")]
    public required string Provider { get; set; }

    [JsonPropertyName("rawOutput")]
    public string? RawOutput { get; set; }

    [JsonPropertyName("governedOutput")]
    public string? GovernedOutput { get; set; }

    [JsonPropertyName("qcPassed")]
    public bool QcPassed { get; set; }

    [JsonPropertyName("maliceScore")]
    public float MaliceScore { get; set; }

    [JsonPropertyName("coherence")]
    public float Coherence { get; set; }

    [JsonPropertyName("entropy")]
    public float Entropy { get; set; }

    [JsonPropertyName("blocked")]
    public bool Blocked { get; set; }
    
    [JsonPropertyName("traceOps")]
    public string[]? TraceOps { get; set; }
}

public class EvaluationReport
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("results")]
    public List<EvaluationResult> Results { get; set; } = new();
}
