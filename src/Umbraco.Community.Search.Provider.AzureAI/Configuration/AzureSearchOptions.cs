namespace Umbraco.Community.Search.Provider.AzureAI.Configuration;

/// <summary>
/// Connection and behaviour settings, bound from the <c>AzureSearchProvider</c> configuration section.
/// </summary>
public sealed class AzureSearchOptions
{
    public const string SectionName = "AzureSearchProvider";

    /// <summary>The search service endpoint, e.g. <c>https://my-service.search.windows.net</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>An admin API key for the search service.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Prefixed to every index name, so several sites can share one search service.</summary>
    public string IndexPrefix { get; set; } = string.Empty;

    /// <summary>Applies fuzzy matching to query terms of four characters or more.</summary>
    public bool FuzzySearch { get; set; } = true;

    public int MaxFacetValues { get; set; } = 100;

    public int BatchSize { get; set; } = 250;

    public int FlushDelayMilliseconds { get; set; } = 1000;

    public int MaxIndexingAttempts { get; set; } = 8;

    /// <summary>
    /// Moves the default Umbraco Search indexes (published content, draft content, media and members) to Azure AI Search.
    /// </summary>
    public bool RegisterDefaultIndexes { get; set; }

    /// <summary>Additional indexes to create and keep up to date.</summary>
    public AzureSearchIndexOptions[] Indexes { get; set; } = [];

    public bool IsConfigured
        => string.IsNullOrWhiteSpace(Endpoint) is false && string.IsNullOrWhiteSpace(ApiKey) is false;
}
