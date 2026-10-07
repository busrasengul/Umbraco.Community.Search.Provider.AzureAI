using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using static Umbraco.Community.Search.Provider.AzureAI.Tests.TestHelpers;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Services;

public class AzureSearchIndexManagerTests
{
    private static AzureSearchIndexManager Manager(AzureSearchOptions options, AzureSearchProviderStatus? status = null, params AzureSearchFieldOptions.Field[] fields)
        => new(Wrap(options), SchemaProvider(options, globalFields: fields), status ?? Enabled, NullLogger<AzureSearchIndexManager>.Instance);

    [TestCase("", "Umb_PublishedContent", "umb-publishedcontent")]
    [TestCase("Dev_", "Products", "dev-products")]
    [TestCase("site1-", "My_Index", "site1-my-index")]
    public void Index_Name_Is_Lowercase_With_Dashes(string prefix, string alias, string expected)
        => Assert.That(Manager(Options(options => options.IndexPrefix = prefix)).IndexName(alias), Is.EqualTo(expected));

    [Test]
    public void Builds_The_Core_Fields_And_Scoring_Profile()
    {
        SearchIndex index = Manager(Options()).BuildIndex("Products");

        Assert.Multiple(() =>
        {
            Assert.That(index.Name, Is.EqualTo("products"));
            Assert.That(index.Fields.Single(field => field.IsKey == true).Name, Is.EqualTo("id"));
            Assert.That(index.Fields.Select(field => field.Name), Is.SupersetOf(new[] { "key", "culture", "accessKeys", "pathKeys", "textsR1", "texts" }));
            Assert.That(index.DefaultScoringProfile, Is.EqualTo("umbraco"));
        });
    }

    [Test]
    public void Declared_Fields_Get_Matching_Azure_Field_Types()
    {
        SearchIndex index = Manager(
            Options(),
            null,
            Field("brand", AzureFieldValues.Keywords, facetable: true, sortable: true),
            Field("price", AzureFieldValues.Decimals, facetable: true, sortable: true),
            Field("sizes", AzureFieldValues.Integers),
            Field("subtitle", AzureFieldValues.Texts)).BuildIndex("Products");

        SearchField Get(string name) => index.Fields.Single(field => field.Name == name);

        Assert.Multiple(() =>
        {
            Assert.That(Get("k_brand").Type, Is.EqualTo(SearchFieldDataType.Collection(SearchFieldDataType.String)));
            Assert.That(Get("k_brand").IsFacetable, Is.True);
            Assert.That(Get("k_brand_sort").IsSortable, Is.True);
            Assert.That(Get("d_price").Type, Is.EqualTo(SearchFieldDataType.Double));
            Assert.That(Get("d_price").IsSortable, Is.True);
            Assert.That(Get("i_sizes").Type, Is.EqualTo(SearchFieldDataType.Collection(SearchFieldDataType.Int32)));
            Assert.That(Get("x_subtitle").IsSearchable, Is.True);
        });
    }

    [Test]
    public void Search_Client_Is_Not_Created_When_Disabled()
    {
        AzureSearchIndexManager manager = Manager(Options(), Disabled);

        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetSearchClient("Products"));
        Assert.That(exception!.Message, Does.Contain("AzureSearchProvider:Endpoint is missing"));
    }
}
