using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using static Umbraco.Community.Search.Provider.AzureAI.Tests.TestHelpers;
using CoreSearchResult = Umbraco.Cms.Search.Core.Models.Searching.SearchResult;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Services;

public class AzureSearchSearcherTests
{
    private readonly List<(string Text, SearchOptions Options)> _requests = [];
    private Mock<SearchClient> _client = null!;

    private static readonly AzureSearchSchema Schema = new(
    [
        Field("category", AzureFieldValues.Keywords, facetable: true),
        Field("price", AzureFieldValues.Decimals, facetable: true, sortable: true),
        Field("sizes", AzureFieldValues.Integers, facetable: true),
    ]);

    [SetUp]
    public void SetUp()
    {
        _requests.Clear();
        _client = new Mock<SearchClient>();
        _client
            .Setup(client => client.SearchAsync<SearchDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, SearchOptions, CancellationToken>((text, options, _) => _requests.Add((text, options)))
            .ReturnsAsync(() => Response.FromValue(
                SearchModelFactory.SearchResults<SearchDocument>(
                    [SearchModelFactory.SearchResult(new SearchDocument { ["key"] = Guid.Empty.ToString("D"), ["objectType"] = "Document" }, 1, null)],
                    totalCount: 1,
                    facets: null,
                    coverage: null,
                    rawResponse: Mock.Of<Response>()),
                Mock.Of<Response>()));
    }

    private AzureSearchSearcher Searcher(AzureSearchProviderStatus? status = null, bool fuzzy = true)
    {
        var indexManager = new Mock<IAzureSearchIndexManager>();
        indexManager.Setup(manager => manager.GetSearchClient(It.IsAny<string>())).Returns(_client.Object);
        indexManager.Setup(manager => manager.IndexName(It.IsAny<string>())).Returns<string>(alias => alias.ToLowerInvariant());

        var schemaProvider = new Mock<IAzureSearchSchemaProvider>();
        schemaProvider.Setup(provider => provider.GetSchema(It.IsAny<string>())).Returns(Schema);

        return new AzureSearchSearcher(
            indexManager.Object,
            schemaProvider.Object,
            status ?? Enabled,
            Wrap(Options(options => options.FuzzySearch = fuzzy)),
            NullLogger<AzureSearchSearcher>.Instance);
    }

    [Test]
    public async Task Empty_Query_Searches_Everything()
    {
        CoreSearchResult result = await Searcher().SearchAsync("Products");

        Assert.That(_requests.Single().Text, Is.EqualTo("*"));
        Assert.That(result.Total, Is.EqualTo(1));
        Assert.That(result.Documents.Single().ObjectType, Is.EqualTo(Umbraco.Cms.Core.Models.UmbracoObjectTypes.Document));
    }

    [Test]
    public async Task Fuzzy_Search_Escapes_Terms_And_Adds_Fuzziness_To_Long_Terms()
    {
        await Searcher().SearchAsync("Products", "cloud c# api");

        Assert.That(_requests.Single().Text, Is.EqualTo("cloud~1 c# api"));
        Assert.That(_requests.Single().Options.QueryType, Is.EqualTo(SearchQueryType.Full));
    }

    [Test]
    public async Task Plain_Search_Passes_The_Query_Through()
    {
        await Searcher(fuzzy: false).SearchAsync("Products", "cloud hosting");

        Assert.That(_requests.Single().Text, Is.EqualTo("cloud hosting"));
        Assert.That(_requests.Single().Options.QueryType, Is.EqualTo(SearchQueryType.Simple));
    }

    [Test]
    public async Task Anonymous_Searches_Exclude_Protected_Content()
    {
        await Searcher().SearchAsync("Products", culture: "en-US");

        Assert.That(_requests.Single().Options.Filter, Is.EqualTo("(culture eq 'inv' or culture eq 'en-us') and not accessKeys/any()"));
    }

    [Test]
    public async Task Members_See_Content_Protected_For_Their_Groups()
    {
        var member = Guid.NewGuid();
        var group = Guid.NewGuid();

        await Searcher().SearchAsync("Products", accessContext: new AccessContext(member, [group]));

        Assert.That(_requests.Single().Options.Filter, Does.Contain($"accessKeys/any(a: search.in(a, '{member:D},{group:D}', ','))"));
    }

    [Test]
    public async Task Filters_Translate_To_OData()
    {
        await Searcher().SearchAsync(
            "Products",
            filters:
            [
                new KeywordFilter("category", ["Design", "O'Reilly"], false),
                new DecimalRangeFilter("price", [new(10m, 50m)], false),
                new IntegerExactFilter("sizes", [1, 2], true),
                new KeywordFilter("notDeclared", ["x"], false),
            ]);

        Assert.That(_requests[0].Options.Filter, Is.EqualTo(
            "culture eq 'inv' and not accessKeys/any()"
            + " and (k_category/any(x: search.in(x, 'Design|O''Reilly', '|')))"
            + " and ((d_price ge 10 and d_price lt 50))"
            + " and not (i_sizes/any(x: (x eq 1) or (x eq 2)))"));
    }

    [Test]
    public async Task Facets_On_Filtered_Fields_Are_Calculated_Without_Their_Own_Filter()
    {
        await Searcher().SearchAsync(
            "Products",
            filters: [new KeywordFilter("category", ["Design"], false)],
            facets: [new KeywordFacet("category"), new DecimalRangeFacet("price", [new("cheap", null, 50m), new("dear", 50m, null)])]);

        Assert.That(_requests, Has.Count.EqualTo(2));
        Assert.That(_requests[0].Options.Facets, Is.EquivalentTo(new[] { "k_category,count:100", "d_price,values:50" }));
        Assert.That(_requests[1].Options.Filter, Does.Not.Contain("k_category"));
        Assert.That(_requests[1].Options.Size, Is.EqualTo(0));
    }

    [Test]
    public async Task Sorters_Use_Sortable_Fields_Only()
    {
        await Searcher().SearchAsync(
            "Products",
            sorters: [new DecimalSorter("price", Direction.Ascending), new KeywordSorter("category", Direction.Ascending), new ScoreSorter(Direction.Descending)]);

        Assert.That(_requests.Single().Options.OrderBy, Is.EqualTo(new[] { "d_price asc", "search.score() desc" }));
    }

    [Test]
    public async Task Facets_Filters_And_Sorters_Without_A_Field_Name_Are_Ignored()
    {
        CoreSearchResult result = await Searcher().SearchAsync(
            "Products",
            filters: [new KeywordFilter(null!, ["x"], false)],
            facets: [new KeywordFacet(null!), new KeywordFacet("")],
            sorters: [new KeywordSorter(null!, Direction.Ascending)]);

        Assert.That(result.Total, Is.EqualTo(1));
        Assert.That(_requests.Single().Options.Facets, Is.Empty);
        Assert.That(_requests.Single().Options.OrderBy, Is.Empty);
    }

    [Test]
    public async Task Disabled_Provider_Returns_No_Results()
    {
        CoreSearchResult result = await Searcher(Disabled).SearchAsync("Products", "cloud");

        Assert.That(result.Total, Is.Zero);
        Assert.That(_requests, Is.Empty);
    }

    [Test]
    public async Task Azure_Failures_Return_No_Results()
    {
        _client
            .Setup(client => client.SearchAsync<SearchDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "Forbidden"));

        CoreSearchResult result = await Searcher().SearchAsync("Products", "cloud");

        Assert.That(result.Total, Is.Zero);
    }
}
