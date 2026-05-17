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
}
