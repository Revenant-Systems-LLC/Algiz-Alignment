using SageRage.Governance;
using SageRage.Infrastructure;

namespace SageRage.Api.Services;

/// <summary>
/// Resolves an ILLMProvider from a GovernanceProfile at runtime.
/// Extend this as new providers are added to Core.
/// </summary>
public static class ProviderFactoryHelper
{
    public static ILLMProvider Create(GovernanceProfile profile)
    {
        throw new NotImplementedException(
            $"Provider factory not yet wired for provider type '{profile.ProviderType}'. " +
            $"Register concrete provider implementations here.");
    }
}
