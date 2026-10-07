using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Models.Configuration;

namespace Umbraco.Community.Search.Provider.AzureAI.Services;

/// <summary>
/// Ensures the Azure indexes behind every Umbraco Search registration that uses this provider.
/// </summary>
internal sealed class AzureSearchIndexInitializer
{
    private readonly IAzureSearchIndexManager _indexManager;
    private readonly IndexOptions _indexOptions;
    private readonly ILogger<AzureSearchIndexInitializer> _logger;

    public AzureSearchIndexInitializer(IAzureSearchIndexManager indexManager, IOptions<IndexOptions> indexOptions, ILogger<AzureSearchIndexInitializer> logger)
    {
        _indexManager = indexManager;
        _indexOptions = indexOptions.Value;
        _logger = logger;
    }

    public IEnumerable<string> IndexAliases
        => _indexOptions.GetContentIndexRegistrations()
            .Where(registration => registration.Indexer == typeof(IAzureSearchIndexer))
            .Select(registration => registration.IndexAlias);

    public async Task EnsureAsync(Func<string, bool> include)
    {
        foreach (var indexAlias in IndexAliases.Where(include))
        {
            try
            {
                await _indexManager.EnsureAsync(indexAlias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure AI Search index {IndexAlias} could not be prepared. {Reason}", indexAlias, ex.Message);
            }
        }
    }
}
