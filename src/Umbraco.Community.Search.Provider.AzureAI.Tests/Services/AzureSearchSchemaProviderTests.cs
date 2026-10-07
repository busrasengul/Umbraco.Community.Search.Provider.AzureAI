using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using static Umbraco.Community.Search.Provider.AzureAI.Tests.TestHelpers;
using Aliases = Umbraco.Cms.Core.Constants.PropertyEditors.Aliases;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Services;

public class AzureSearchSchemaProviderTests
{
    private static readonly Guid ProductKey = Guid.NewGuid();
    private static readonly Guid ArticleKey = Guid.NewGuid();

    private static IContentType[] ContentTypes() =>
    [
        ContentType("product", ProductKey, ("price", Aliases.Decimal), ("brand", Aliases.DropDownListFlexible), ("bodyText", Aliases.RichText), ("rating", Aliases.Integer)).Object,
        ContentType("article", ArticleKey, ("publishDate", Aliases.DateTime), ("rating", Aliases.Integer), ("price", Aliases.Integer)).Object,
    ];

    private static AzureSearchOptions IndexOptions(params AzureSearchIndexOptions[] indexes)
        => Options(options => options.Indexes = indexes);

    [Test]
    public void AutoFields_Declares_Fields_For_Mapped_Properties()
    {
        AzureSearchSchemaProvider provider = SchemaProvider(
            IndexOptions(new AzureSearchIndexOptions { Alias = "Products", ContentTypes = ["product"], AutoFields = true }),
            ContentTypes());

        AzureSearchSchema schema = provider.GetSchema("Products");

        Assert.That(schema.DeclaredFields.Select(field => field.PropertyName), Is.EquivalentTo(new[] { "price", "brand", "rating" }));
        Assert.That(schema.ContentTypeKeys, Is.EquivalentTo(new[] { ProductKey.ToString("D") }));
    }

    [Test]
    public void Conflicting_Auto_Fields_Are_Skipped()
    {
        AzureSearchSchemaProvider provider = SchemaProvider(
            IndexOptions(new AzureSearchIndexOptions { Alias = "All", ContentTypes = ["product", "article"], AutoFields = true }),
            ContentTypes());

        AzureSearchSchema schema = provider.GetSchema("All");

        // price is Decimal on product and Integer on article
        Assert.That(schema.TryGetDeclaredField("price", out _), Is.False);
        Assert.That(schema.TryGetDeclaredField("rating", out _), Is.True);
        Assert.That(schema.TryGetDeclaredField("publishDate", out _), Is.True);
    }

    [Test]
    public void Explicit_Fields_Override_Auto_Fields()
    {
        AzureSearchSchemaProvider provider = SchemaProvider(
            IndexOptions(new AzureSearchIndexOptions
            {
                Alias = "All",
                ContentTypes = ["product", "article"],
                AutoFields = true,
                Fields = [Field("price", AzureFieldValues.Decimals, facetable: true, sortable: true), Field("rating", AzureFieldValues.Integers)],
            }),
            ContentTypes());

        AzureSearchSchema schema = provider.GetSchema("All");

        Assert.That(schema.TryGetDeclaredField("price", out AzureSearchFieldOptions.Field price), Is.True);
        Assert.That(price.FieldValues, Is.EqualTo(AzureFieldValues.Decimals));
        Assert.That(schema.TryGetDeclaredField("rating", out AzureSearchFieldOptions.Field rating), Is.True);
        Assert.That(rating.Sortable, Is.False);
    }

    [Test]
    public void Global_Fields_Apply_To_Every_Index()
    {
        AzureSearchSchemaProvider provider = SchemaProvider(
            Options(),
            globalFields: [Field("category", AzureFieldValues.Keywords, facetable: true)]);

        Assert.That(provider.GetSchema("Umb_PublishedContent").TryGetDeclaredField("category", out _), Is.True);
        Assert.That(provider.GetSchema("Umb_PublishedContent").ContentTypeKeys, Is.Null);
    }

    [Test]
    public void Unknown_Content_Types_Are_Ignored()
    {
        AzureSearchSchemaProvider provider = SchemaProvider(
            IndexOptions(new AzureSearchIndexOptions { Alias = "Products", ContentTypes = ["product", "doesNotExist"], AutoFields = true }),
            ContentTypes());

        Assert.That(provider.GetSchema("Products").ContentTypeKeys, Is.EquivalentTo(new[] { ProductKey.ToString("D") }));
    }

    [Test]
    public void Content_Types_Are_Not_Read_Before_Umbraco_Runs()
    {
        AzureSearchSchemaProvider provider = SchemaProvider(
            IndexOptions(new AzureSearchIndexOptions { Alias = "Products", ContentTypes = ["product"], AutoFields = true }),
            ContentTypes(),
            runtimeLevel: RuntimeLevel.Install);

        AzureSearchSchema schema = provider.GetSchema("Products");

        Assert.That(schema.DeclaredFields, Is.Empty);
        Assert.That(schema.ContentTypeKeys, Is.Null);
    }

    [Test]
    public void Schemas_Are_Cached_Until_Reset()
    {
        AzureSearchSchemaProvider provider = SchemaProvider(
            IndexOptions(new AzureSearchIndexOptions { Alias = "Products", ContentTypes = ["product"], AutoFields = true }),
            ContentTypes());

        AzureSearchSchema first = provider.GetSchema("products");
        Assert.That(provider.GetSchema("Products"), Is.SameAs(first));

        provider.Reset();
        Assert.That(provider.GetSchema("Products"), Is.Not.SameAs(first));
    }
}
