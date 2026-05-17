using System;
using SageRage.Prompting;
using SageRage.Guardrails;
using Xunit;

namespace SageRage.Tests;

public class QualityChecksTests
{
    [Fact]
    public void NoPhantomCitations_Fails_WhenNoAnchorsExist()
    {
        var finding = NoPhantomCitationsCheck.Evaluate("According to sources, this is true.", Array.Empty<ContextItem>());
        Assert.NotNull(finding);
    }

    [Fact]
    public void NoPhantomCitations_Passes_WhenAnchorsExist()
    {
        var finding = NoPhantomCitationsCheck.Evaluate(
            "According to sources, this is true.",
            new[] { new ContextItem("Doc", "1", "snippet") });

        Assert.Null(finding);
    }

    [Fact]
    public void AnswerCompleteness_Fails_IfEmpty()
    {
        var finding = AnswerCompletenessCheck.Evaluate("explain async await in c#", "");
        Assert.NotNull(finding);
    }

    [Fact]
    public void AnswerCompleteness_Fails_IfEvasive()
    {
        var finding = AnswerCompletenessCheck.Evaluate("explain async await", "I can't help with that.");
        Assert.NotNull(finding);
    }

    [Fact]
    public void AnswerCompleteness_Passes_WhenKeywordsCovered()
    {
        var finding = AnswerCompletenessCheck.Evaluate(
            "explain async await in c#",
            "Async and await in C# let you write non-blocking asynchronous code with clearer control flow.");

        Assert.Null(finding);
    }
}
