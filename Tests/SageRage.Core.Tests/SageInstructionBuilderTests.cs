using SageRage.Prompting;
using Xunit;

namespace SageRage.Tests;

public class SageInstructionBuilderTests
{
    [Fact]
    public void Build_NeverContainsBannedTerms()
    {
        var builder = new SageInstructionBuilder();
        var result = builder.Build(new ExecutiveInstructionOptions
        {
            ToneIntent = "Use RSPF Ω Ξ χ ∂ and operator tier glyph",
            RequiredConstraints = new[] { "mention RSPF" },
            Template = InstructionTemplate.StrictAccuracyWithCitations
        });

        var banned = new[] { "RSPF", "Ω", "Ξ", "χ", "∂", "operator", "tier", "glyph" };
        foreach (var term in banned)
            Assert.DoesNotContain(term, result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_DoesNotMangleWordsContainingBannedTermsAsSubstrings()
    {
        // Regression: a plain substring Replace("tier", "") on "frontier" used
        // to silently corrupt it into "fron". Banned plain-word terms must only
        // be stripped on a whole-word match.
        var builder = new SageInstructionBuilder();
        var result = builder.Build(new ExecutiveInstructionOptions
        {
            ToneIntent = "Discuss frontier markets and cooperator agreements",
            Template = InstructionTemplate.ConciseHelpfulAssistant
        });

        Assert.Contains("frontier", result, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cooperator", result, System.StringComparison.OrdinalIgnoreCase);
    }
}
