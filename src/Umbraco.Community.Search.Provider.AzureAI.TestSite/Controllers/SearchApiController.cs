using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;

namespace Umbraco.Community.Search.Provider.AzureAI.TestSite.Controllers;

/// <summary>
/// A small JSON API for trying the provider against the demo content.
/// </summary>
[ApiController]
[Route("api/search")]
public sealed class SearchApiController : ControllerBase
{
    private const string DefaultIndex = "TestSite_Content";

    private static readonly string[] KeywordFields = ["category", "brand", "audience", "regions", "tags"];

    private readonly ISearcherResolver _searcherResolver;
    private readonly IIndexerResolver _indexerResolver;
    private readonly IDistributedContentIndexRebuilder _rebuilder;
    private readonly IPublishedContentCache _contentCache;
    private readonly IWebHostEnvironment _environment;

    public SearchApiController(
        ISearcherResolver searcherResolver,
        IIndexerResolver indexerResolver,
        IDistributedContentIndexRebuilder rebuilder,
        IPublishedContentCache contentCache,
        IWebHostEnvironment environment)
    {
        _searcherResolver = searcherResolver;
        _indexerResolver = indexerResolver;
        _rebuilder = rebuilder;
        _contentCache = contentCache;
        _environment = environment;
    }

    /// <summary>
    /// Searches an index, e.g. <c>/api/search?q=cloud&amp;category=Technology&amp;sort=price-asc</c>.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string index = DefaultIndex,
        [FromQuery] string? sort = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        ISearcher? searcher = _searcherResolver.GetSearcher(index);
        if (searcher is null)
        {
            return IndexNotRegistered(index);
        }

        Filter[] filters = KeywordFields
            .Select(field => (Field: field, Values: Request.Query[field].Where(value => string.IsNullOrWhiteSpace(value) is false).Select(value => value!).ToArray()))
            .Where(selection => selection.Values.Length > 0)
            .Select(selection => (Filter)new KeywordFilter(selection.Field, selection.Values, false))
            .ToArray();

        Facet[] facets =
        [
            .. KeywordFields.Select(field => new KeywordFacet(field)),
            new IntegerExactFacet("rating"),
            new DecimalRangeFacet("price", [new("under-50", null, 50m), new("50-200", 50m, 200m), new("200-plus", 200m, null)]),
        ];

        Sorter[] sorters = sort switch
        {
            "price-asc" => [new DecimalSorter("price", Direction.Ascending)],
            "price-desc" => [new DecimalSorter("price", Direction.Descending)],
            "newest" => [new DateTimeOffsetSorter("publishDate", Direction.Descending)],
            _ => [new ScoreSorter(Direction.Descending)],
        };

        SearchResult result = await searcher.SearchAsync(index, q, filters, facets, sorters, skip: skip, take: take);

        return Ok(new
        {
            result.Total,
            Hits = result.Documents.Select(document =>
            {
                IPublishedContent? content = _contentCache.GetById(document.Id);
                return new { document.Id, content?.Name, ContentType = content?.ContentType.Alias };
            }),
            Facets = result.Facets.Select(facet => new
            {
                facet.FieldName,
                Values = facet.Values.Select(value => value switch
                {
                    KeywordFacetValue keyword => new { Key = keyword.Key, keyword.Count },
                    IntegerExactFacetValue integer => new { Key = integer.Key.ToString(), integer.Count },
                    DecimalRangeFacetValue range => new { Key = range.Key, range.Count },
                    _ => new { Key = value.ToString() ?? string.Empty, Count = 0L },
                }),
            }),
        });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] string index = DefaultIndex)
    {
        IIndexer? indexer = _indexerResolver.GetIndexer(index);
        if (indexer is null)
        {
            return IndexNotRegistered(index);
        }

        IndexMetadata metadata = await indexer.GetMetadataAsync(index);
        return Ok(new { index, metadata.DocumentCount, HealthStatus = metadata.HealthStatus.ToString(), metadata.ProviderName });
    }

    private NotFoundObjectResult IndexNotRegistered(string index)
        => NotFound(new { error = $"No index '{index}' is registered. If it is an Azure AI Search index, check the startup log for AzureSearchProvider configuration errors." });

    [HttpPost("rebuild")]
    public IActionResult Rebuild([FromQuery] string index = DefaultIndex)
        => _environment.IsDevelopment()
            ? Ok(new { index, started = _rebuilder.Rebuild(index) })
            : NotFound();
}
