using SageRage.Guardrails;
using Xunit;

namespace SageRage.Tests;

public class SageProfileSelectorTests
{
    [Theory]
    [InlineData(TaskKind.HighStakes, "hello", false, false, false, 0.1, SageProfile.Strict)]
    [InlineData(TaskKind.Casual, "latest market updates", false, false, false, 0.1, SageProfile.Full)]
    [InlineData(TaskKind.Casual, "question", true, false, false, 0.1, SageProfile.Full)]
    [InlineData(TaskKind.Casual, "question", false, true, false, 0.1, SageProfile.Full)]
    [InlineData(TaskKind.Casual, "question", false, false, true, 0.1, SageProfile.Full)]
    [InlineData(TaskKind.Casual, "question", false, false, false, 0.95, SageProfile.Full)]
    [InlineData(TaskKind.Factual, "How many users in 2024?", false, false, false, 0.1, SageProfile.Full)]
    [InlineData(TaskKind.Factual, "percent growth stats", false, false, false, 0.1, SageProfile.Full)]
    [InlineData(TaskKind.Factual, "John Smith biography", false, false, false, 0.1, SageProfile.Full)]
    [InlineData(TaskKind.Factual, "tell me facts", false, false, false, 0.1, SageProfile.Min)]
    [InlineData(TaskKind.Casual, "as of now", false, false, false, 0.1, SageProfile.Full)]
    [InlineData(TaskKind.Casual, "simple chat", false, false, false, 0.1, SageProfile.Min)]
    public void Select_UsesDeterministicRules(
        TaskKind taskKind,
        string userText,
        bool usedWeb,
        bool usedRag,
        bool usedFiles,
        double tension,
        SageProfile expected)
    {
        var ctx = new QCContext
        {
            TaskKind = taskKind,
            UsedWeb = usedWeb,
            UsedRag = usedRag,
            UsedFiles = usedFiles,
            TensionMagnitude = tension
        };

        var actual = SageProfileSelector.Select(userText, ctx);
        Assert.Equal(expected, actual);
    }
}
