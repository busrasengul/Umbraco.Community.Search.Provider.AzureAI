using Umbraco.Cms.Core.Models;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Configuration;

public class AzureSearchOptionsValidatorTests
{
    private static AzureSearchOptions ValidOptions() => new()
    {
        Endpoint = "https://my-service.search.windows.net",
        ApiKey = "key",
    };

    [Test]
    public void Valid_Configuration_Has_No_Errors()
    {
        AzureSearchOptions options = ValidOptions();
        options.IndexPrefix = "dev-";
        options.Indexes =
        [
            new() { Alias = "Products", ContentTypes = ["product"], AutoFields = true },
            new() { Alias = "Media_Library", ObjectTypes = [UmbracoObjectTypes.Media], ContentState = AzureIndexContentState.Draft },
        ];

        Assert.That(AzureSearchOptionsValidator.Validate(options), Is.Empty);
    }

    [Test]
    public void Missing_Endpoint_And_ApiKey_Are_Reported()
    {
        IReadOnlyList<string> errors = AzureSearchOptionsValidator.Validate(new AzureSearchOptions());

        Assert.That(errors, Has.Some.Contains("AzureSearchProvider:Endpoint is missing"));
        Assert.That(errors, Has.Some.Contains("AzureSearchProvider:ApiKey is missing"));
    }

    [TestCase("http://my-service.search.windows.net")]
    [TestCase("my-service.search.windows.net")]
    [TestCase("not a url")]
    public void Endpoint_Must_Be_An_Absolute_Https_Url(string endpoint)
    {
        AzureSearchOptions options = ValidOptions();
        options.Endpoint = endpoint;

        Assert.That(AzureSearchOptionsValidator.Validate(options), Has.Some.Contains("must be an absolute https URL"));
    }

    [TestCase("bad prefix")]
    [TestCase("-dev")]
    public void Invalid_IndexPrefix_Is_Reported(string prefix)
    {
        AzureSearchOptions options = ValidOptions();
        options.IndexPrefix = prefix;

        Assert.That(AzureSearchOptionsValidator.Validate(options), Has.Some.Contains("IndexPrefix"));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Batch_Size_Must_Be_Positive(int batchSize)
    {
        AzureSearchOptions options = ValidOptions();
        options.BatchSize = batchSize;

        Assert.That(AzureSearchOptionsValidator.Validate(options), Has.Some.Contains("BatchSize must be greater than zero"));
    }

    [Test]
    public void Index_Alias_Is_Required_And_Unique()
    {
        AzureSearchOptions options = ValidOptions();
        options.Indexes = [new() { Alias = "" }, new() { Alias = "Products" }, new() { Alias = "products" }];

        IReadOnlyList<string> errors = AzureSearchOptionsValidator.Validate(options);

        Assert.That(errors, Has.Some.Contains("Indexes:0:Alias is missing"));
        Assert.That(errors, Has.Some.Contains("Indexes:2:Alias 'products' is declared more than once"));
    }

    [TestCase("my index")]
    [TestCase("products!")]
    [TestCase("products-")]
    public void Index_Alias_Must_Give_A_Valid_Azure_Index_Name(string alias)
    {
        AzureSearchOptions options = ValidOptions();
        options.Indexes = [new() { Alias = alias }];

        Assert.That(AzureSearchOptionsValidator.Validate(options), Has.Some.Contains("gives the Azure index name"));
    }

    [Test]
    public void Media_Indexes_Must_Use_Draft_Content_State()
    {
        AzureSearchOptions options = ValidOptions();
        options.Indexes = [new() { Alias = "Media", ObjectTypes = [UmbracoObjectTypes.Media] }];

        Assert.That(AzureSearchOptionsValidator.Validate(options), Has.Some.Contains("ContentState must be Draft"));
    }

    [Test]
    public void Unsupported_Object_Types_Are_Reported()
    {
        AzureSearchOptions options = ValidOptions();
        options.Indexes = [new() { Alias = "Types", ObjectTypes = [UmbracoObjectTypes.DocumentType], ContentState = AzureIndexContentState.Draft }];

        Assert.That(AzureSearchOptionsValidator.Validate(options), Has.Some.Contains("Only Document, Media and Member can be indexed"));
    }

    [Test]
    public void AutoFields_Requires_Content_Types()
    {
        AzureSearchOptions options = ValidOptions();
        options.Indexes = [new() { Alias = "Products", AutoFields = true }];

        Assert.That(AzureSearchOptionsValidator.Validate(options), Has.Some.Contains("AutoFields needs ContentTypes"));
    }

    [Test]
    public void Field_Declarations_Are_Validated()
    {
        AzureSearchFieldOptions.Field[] fields =
        [
            new() { PropertyName = "price", FieldValues = AzureFieldValues.Decimals },
            new() { PropertyName = "Price", FieldValues = AzureFieldValues.Decimals },
            new() { PropertyName = "1st", FieldValues = AzureFieldValues.Keywords },
            new() { PropertyName = "summary", FieldValues = AzureFieldValues.Texts, Facetable = true },
        ];

        IReadOnlyList<string> errors = AzureSearchOptionsValidator.Validate(ValidOptions(), fields);

        Assert.That(errors, Has.Some.Contains("'Price' is declared more than once"));
        Assert.That(errors, Has.Some.Contains("'1st' may only contain letters"));
        Assert.That(errors, Has.Some.Contains("('summary') is Texts"));
    }
}
