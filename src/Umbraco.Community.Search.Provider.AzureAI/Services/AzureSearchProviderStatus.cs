namespace Umbraco.Community.Search.Provider.AzureAI.Services;

/// <summary>
/// Whether the provider has a usable configuration. When it does not, indexing is skipped and searches return no results,
/// so a missing or broken configuration never takes the site down.
/// </summary>
public sealed class AzureSearchProviderStatus
{
    public AzureSearchProviderStatus(IEnumerable<string> configurationErrors)
        => ConfigurationErrors = configurationErrors.ToArray();

    public IReadOnlyList<string> ConfigurationErrors { get; }

    public bool IsEnabled => ConfigurationErrors.Count is 0;

    internal string DisabledMessage
        => $"The Azure AI Search provider is disabled because of configuration errors: {string.Join(" ", ConfigurationErrors)}";
}
