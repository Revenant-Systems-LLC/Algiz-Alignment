using System;
using SageRage.Infrastructure;
using Xunit;

namespace SageRage.Tests;

public class GeminiLLMProviderTests
{
    [Fact]
    public void Constructor_Throws_WhenNoKeyAndNoEnvVar()
    {
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
        Assert.Throws<InvalidOperationException>(() => new GeminiLLMProvider(null));
    }

    [Fact]
    public void Constructor_UsesEnvVar_WhenConstructorKeyMissing()
    {
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "env-secret");
        var provider = new GeminiLLMProvider(null, model: GeminiLLMProvider.PrimaryModel);
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_Throws_WhenModelIsNotAllowed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new GeminiLLMProvider("secret", model: "gemini-2.5-flash"));
    }

    [Fact]
    public void Constructor_AllowsPrimaryModel()
    {
        var provider = new GeminiLLMProvider("secret", model: GeminiLLMProvider.PrimaryModel);
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_AllowsFallbackModel()
    {
        var provider = new GeminiLLMProvider("secret", model: GeminiLLMProvider.FallbackModel);
        Assert.NotNull(provider);
    }
}
