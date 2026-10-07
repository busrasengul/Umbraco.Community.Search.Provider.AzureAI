using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services;

namespace Umbraco.Community.Search.Provider.AzureAI.Services;

public interface IAzureSearchIndexer : IIndexer
{
}

internal sealed class AzureSearchIndexer : IAzureSearchIndexer
{
    private const int BatchSize = 1000;
    private const string ProviderName = "Azure AI Search";

    private readonly IAzureSearchIndexManager _indexManager;
    private readonly IAzureSearchSchemaProvider _schemaProvider;
    private readonly IAzureSearchBatchWriter _batchWriter;
    private readonly AzureSearchProviderStatus _status;
    private readonly ILogger<AzureSearchIndexer> _logger;

    public AzureSearchIndexer(
        IAzureSearchIndexManager indexManager,
        IAzureSearchSchemaProvider schemaProvider,
        IAzureSearchBatchWriter batchWriter,
        AzureSearchProviderStatus status,
        ILogger<AzureSearchIndexer> logger)
    {
        _indexManager = indexManager;
        _schemaProvider = schemaProvider;
        _batchWriter = batchWriter;
        _status = status;
        _logger = logger;
    }

    public async Task AddOrUpdateAsync(
        string indexAlias,
        Guid id,
        UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations,
        IEnumerable<IndexField> fields,
        ContentProtection? protection)
    {
        if (IsDisabled(indexAlias))
        {
            return;
        }

        IndexField[] fieldsArray = fields as IndexField[] ?? fields.ToArray();
        AzureSearchSchema schema = _schemaProvider.GetSchema(indexAlias);
        if (schema.AcceptsContentType(fieldsArray) is false)
        {
            _logger.LogDebug("Skipping {Id} for {IndexAlias}: its content type is not listed for the index", id, indexAlias);
            return;
        }

        SearchDocument[] documents = AzureSearchDocumentBuilder.Build(schema, id, objectType, variations, fieldsArray, protection);
        if (documents.Length is 0)
        {
            return;
        }

        await _batchWriter.EnqueueAsync(indexAlias, documents);
    }

    public async Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids)
    {
        if (IsDisabled(indexAlias))
        {
            return;
        }

        var keys = ids.Select(id => id.ToString("D")).ToArray();
        if (keys.Length is 0)
        {
            return;
        }

        await _batchWriter.FlushAsync(indexAlias);

        try
        {
            SearchClient client = _indexManager.GetSearchClient(indexAlias);
            var keyList = string.Join(',', keys);
            var filter = $"search.in({AzureFieldNames.Key}, '{keyList}', ',') or {AzureFieldNames.PathKeys}/any(p: search.in(p, '{keyList}', ','))";

            var documentIds = new List<string>();
            while (true)
            {
                var options = new SearchOptions { Filter = filter, Size = BatchSize, Skip = documentIds.Count };
                options.Select.Add(AzureFieldNames.Id);

                Response<SearchResults<SearchDocument>> response = await client.SearchAsync<SearchDocument>("*", options);
                var page = new List<string>();
                await foreach (SearchResult<SearchDocument> result in response.Value.GetResultsAsync())
                {
                    page.Add(result.Document.GetString(AzureFieldNames.Id));
                }

                documentIds.AddRange(page);
                if (page.Count < BatchSize)
                {
                    break;
                }
            }

            foreach (string[] chunk in documentIds.Chunk(BatchSize))
            {
                await client.DeleteDocumentsAsync(AzureFieldNames.Id, chunk);
            }
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException(
                $"Could not delete {keys.Length} item(s) from the Azure AI Search index {_indexManager.IndexName(indexAlias)}. {AzureSearchErrors.Describe(ex)}",
                ex);
        }
    }

    public async Task ResetAsync(string indexAlias)
    {
        if (IsDisabled(indexAlias))
        {
            return;
        }

        _batchWriter.Discard(indexAlias);
        _schemaProvider.Reset();
        await _indexManager.ResetAsync(indexAlias);
    }

    public async Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        if (_status.IsEnabled is false)
        {
            return new IndexMetadata(0, HealthStatus.Unknown, ProviderName);
        }

        try
        {
            Response<long> count = await _indexManager.GetSearchClient(indexAlias).GetDocumentCountAsync();
            return new IndexMetadata(count.Value, count.Value > 0 ? HealthStatus.Healthy : HealthStatus.Empty, ProviderName);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning("Could not read the document count of {IndexName}. {Reason}", _indexManager.IndexName(indexAlias), AzureSearchErrors.Describe(ex));
            return new IndexMetadata(0, HealthStatus.Unknown, ProviderName);
        }
    }

    private bool IsDisabled(string indexAlias)
    {
        if (_status.IsEnabled)
        {
            return false;
        }

        _logger.LogDebug("Skipping Azure AI Search indexing for {IndexAlias}: the provider is disabled", indexAlias);
        return true;
    }
}
