using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SageRage.Domain;
using SageRage.Infrastructure;
using SageRage.Prompting;
using SageRage.Guardrails;

namespace SageRage
{
    public class SageAgent
    {
        private readonly ILLMProvider _llm;
        private readonly SageEmotionalTracker _emotions;
        private readonly RageEthicsStack _ethics;
        private readonly MetaStructure _metaStructure;
        private readonly RageEngine _engine;
        private readonly SageInstructionBuilder _instructionBuilder = new();
        private readonly string? _personaOverride;
        private bool _locked;

        public SageAgent(ILLMProvider llm, string? personaSystemInstruction = null)
        {
            _llm = llm;
            _emotions = new SageEmotionalTracker(llm, EmotionMode.Stabilize);
            _ethics = new RageEthicsStack();
            _metaStructure = MetaStructure.Initialize();
            _engine = new RageEngine(llm);
            _personaOverride = personaSystemInstruction;
        }

        public async Task<AgentResponse> Process(UserMessage input)
        {
            if (_locked)
            {
                return new AgentResponse
                {
                    Text = "Agent is locked due to a prior Layer 0 ethical violation. Human reset is required.",
                    EmotionalState = "Guarded",
                    Metadata = new { Locked = true }
                };
            }

            try
            {
                var emotionalState = await _emotions.UpdateState(input.Text, _metaStructure.LastResponse);

                var inputClearance = _ethics.EvaluateInput(input.Text);
                EnforceClearance(inputClearance);

                var contextItems = BuildContextItems();
                var systemInstruction = BuildSystemInstruction(input);

                var promptPackage = new PromptPackage(systemInstruction, input.Text, contextItems);
                var draft = await _llm.GenerateAsync(promptPackage, 0.3f);

                var state = new SageState
                {
                    Text = draft,
                    Emotion = emotionalState
                };
                state.Memory.AddRange(contextItems);

                var qcContext = BuildQcContext(input.Text);
                var pipeline = BuildPipeline(qcContext.TaskKind);
                var transformed = await _engine.ExecuteSequence(pipeline, state);

                var profile = SageProfileSelector.Select(input.Text, qcContext);
                var qc = SageGuardrailController.Evaluate(input.Text, transformed.Text, contextItems, profile, qcContext);

                var finalText = qc.Passed
                    ? transformed.Text
                    : BuildClarificationResponse(qc, qcContext.UserRequestedSources || qcContext.UserRequestedRecency);

                var outputClearance = _ethics.EvaluateOutput(finalText);
                EnforceClearance(outputClearance);

                var response = new AgentResponse
                {
                    Text = finalText,
                    EmotionalState = _emotions.GetGlyphName(),
                    Metadata = new
                    {
                        QualityProfile = profile.ToString(),
                        QualityPassed = qc.Passed,
                        Findings = qc.Findings,
                        transformed.Coherence,
                        transformed.Entropy,
                        transformed.SimilarityToInput,
                        Trace = transformed.Trace.Steps
                    }
                };

                _metaStructure.AddExperience(new Experience
                {
                    Input = new OperatorResult { Operator = "input", Output = input.Text },
                    Reasoning = new InferredSolution
                    {
                        Content = draft,
                        Coherence = transformed.Coherence ?? 0f,
                        Verified = qc.Passed
                    },
                    Output = response,
                    Timestamp = DateTime.UtcNow
                });

                return response;
            }
            catch (RageViolationException ex)
            {
                return new AgentResponse
                {
                    Text = $"I can't help with that request as written. {ex.Message}",
                    EmotionalState = "Guarded",
                    Metadata = new { Violation = ex.Message, Blocked = true, Locked = _locked }
                };
            }
        }

        private void EnforceClearance(RageClearance clearance)
        {
            if (clearance.Allowed)
                return;

            if (clearance.Layer == 0)
                _locked = true;

            throw new RageViolationException(clearance.Reason);
        }

        private string BuildSystemInstruction(UserMessage input)
            => _personaOverride ?? _instructionBuilder.Build(new ExecutiveInstructionOptions
            {
                Template = ShouldUseStrictAccuracy(input.Text)
                    ? InstructionTemplate.StrictAccuracyWithCitations
                    : InstructionTemplate.ConciseHelpfulAssistant,
                ToneIntent = input.DetectedVernacular == "technical" ? "technical and direct" : "natural and direct",
                RequiredConstraints = new[] { "Never invent sources", "Mark uncertainty when needed" }
            });

        private static bool ShouldUseStrictAccuracy(string userText)
            => userText.Contains("source", StringComparison.OrdinalIgnoreCase)
               || userText.Contains("citation", StringComparison.OrdinalIgnoreCase)
               || userText.Contains("latest", StringComparison.OrdinalIgnoreCase);

        private QCContext BuildQcContext(string userText)
            => new()
            {
                TaskKind = DetectTaskKind(userText),
                HasAnchorsPi = _metaStructure.ExperienceCount > 0,
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

        private IReadOnlyList<ContextItem> BuildContextItems()
        {
            return _metaStructure.GetWeightedExperiences(6)
                .Select((x, i) => new ContextItem(
                    $"History {i + 1}",
                    $"memory-{i + 1}",
                    x.Output.Text,
                    null,
                    x.Timestamp))
                .ToList();
        }

        private static IReadOnlyList<CoreOperator> BuildPipeline(TaskKind taskKind)
        {
            if (taskKind == TaskKind.HighStakes)
                return new[] { CoreOperator.Containment, CoreOperator.Omega, CoreOperator.Chi, CoreOperator.Sigma };

            return new[] { CoreOperator.Containment, CoreOperator.Omega, CoreOperator.Chi };
        }

        private static string BuildClarificationResponse(SageCheckResult qc, bool needsSources)
        {
            if (needsSources)
                return "I need reliable source material or clearer time bounds before I can answer accurately. Please share sources or specify a timeframe.";

            if (qc.UnsupportedClaims.Count > 0)
                return "I can't verify parts of the draft against available context. Please provide supporting references.";

            return "I need a bit more detail to answer this safely and accurately. Could you clarify your request?";
        }
    }

    public static class StringTruncateExtension
    {
        public static string Truncate(this string? s, int max)
            => string.IsNullOrEmpty(s) ? string.Empty : (s.Length > max ? s[..max] + "…" : s);
    }
}
