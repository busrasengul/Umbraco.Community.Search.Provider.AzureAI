using Azure.Search.Documents.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using static Umbraco.Community.Search.Provider.AzureAI.Tests.TestHelpers;
using CoreFieldNames = Umbraco.Cms.Search.Core.Constants.FieldNames;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Services;

public class AzureSearchDocumentBuilderTests
{
    private static readonly Guid Id = Guid.Parse("8a3b2f8e-55c4-4d0e-9f53-2b8f6e0a1c11");

    [Test]
    public void Builds_One_Document_Per_Culture_And_Skips_Segments()
    {
        SearchDocument[] documents = AzureSearchDocumentBuilder.Build(
            AzureSearchSchema.Empty,
            Id,
            UmbracoObjectTypes.Document,
            [new("en-US", null), new("da-DK", null), new("en-US", "segment")],
            [],
            null);

        Assert.That(documents.Select(document => document.GetString("id")), Is.EqualTo(new[]
        {
            "8a3b2f8e55c44d0e9f532b8f6e0a1c11-en-us",
            "8a3b2f8e55c44d0e9f532b8f6e0a1c11-da-dk",
        }));
    }

    [Test]
    public void Invariant_Content_Uses_The_Invariant_Culture_Marker()
    {
        SearchDocument document = AzureSearchDocumentBuilder.Build(AzureSearchSchema.Empty, Id, UmbracoObjectTypes.Media, [new(null, null)], [], null).Single();

        Assert.Multiple(() =>
        {
            Assert.That(document.GetString("culture"), Is.EqualTo("inv"));
            Assert.That(document.GetString("key"), Is.EqualTo(Id.ToString("D")));
            Assert.That(document.GetString("objectType"), Is.EqualTo("Media"));
        });
    }

    [Test]
    public void Protection_Becomes_Access_Keys()
    {
        var group = Guid.NewGuid();

        SearchDocument document = AzureSearchDocumentBuilder.Build(
            AzureSearchSchema.Empty, Id, UmbracoObjectTypes.Document, [new(null, null)], [], new ContentProtection([group])).Single();

        Assert.That((string[])document["accessKeys"], Is.EqualTo(new[] { group.ToString("D") }));
    }

    [Test]
    public void Texts_Are_Collected_By_Relevance_For_The_Matching_Culture()
    {
        IndexField[] fields =
        [
            new(CoreFieldNames.Name, new IndexValue { TextsR1 = ["English name"] }, "en-US", null),
            new(CoreFieldNames.Name, new IndexValue { TextsR1 = ["Dansk navn"] }, "da-DK", null),
            new("bodyText", new IndexValue { TextsR2 = ["Heading"], Texts = ["Body"] }, null, null),
        ];

        SearchDocument document = AzureSearchDocumentBuilder.Build(AzureSearchSchema.Empty, Id, UmbracoObjectTypes.Document, [new("en-US", null)], fields, null).Single();

        Assert.Multiple(() =>
        {
            Assert.That((List<string>)document["textsR1"], Is.EqualTo(new[] { "English name" }));
            Assert.That((List<string>)document["textsR2"], Is.EqualTo(new[] { "Heading" }));
            Assert.That((List<string>)document["texts"], Is.EqualTo(new[] { "Body" }));
            Assert.That(document.GetString("name"), Is.EqualTo("English name"));
        });
    }

    [Test]
    public void Declared_Fields_Are_Written_With_Their_Type_Prefix()
    {
        var schema = new AzureSearchSchema(
        [
            Field("brand", AzureFieldValues.Keywords, facetable: true, sortable: true),
            Field("price", AzureFieldValues.Decimals, sortable: true),
            Field("sizes", AzureFieldValues.Integers),
        ]);
        IndexField[] fields =
        [
            new("brand", new IndexValue { Keywords = ["Acme", "Umbri"] }, null, null),
            new("price", new IndexValue { Decimals = [12.5m] }, null, null),
            new("sizes", new IndexValue { Integers = [1, 2] }, null, null),
            new("undeclared", new IndexValue { Integers = [42] }, null, null),
        ];

        SearchDocument document = AzureSearchDocumentBuilder.Build(schema, Id, UmbracoObjectTypes.Document, [new(null, null)], fields, null).Single();

        Assert.Multiple(() =>
        {
            Assert.That((string[])document["k_brand"], Is.EqualTo(new[] { "Acme", "Umbri" }));
            Assert.That(document["k_brand_sort"], Is.EqualTo("Acme"));
            Assert.That(document["d_price"], Is.EqualTo(12.5d));
            Assert.That((int[])document["i_sizes"], Is.EqualTo(new[] { 1, 2 }));
            Assert.That(document.ContainsKey("i_undeclared"), Is.False);
        });
    }
}
