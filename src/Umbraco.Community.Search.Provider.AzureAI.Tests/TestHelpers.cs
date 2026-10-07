using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using CoreFieldNames = Umbraco.Cms.Search.Core.Constants.FieldNames;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests;

internal static class TestHelpers
{
    public static AzureSearchOptions Options(Action<AzureSearchOptions>? configure = null)
    {
        var options = new AzureSearchOptions { Endpoint = "https://test.search.windows.net", ApiKey = "key" };
        configure?.Invoke(options);
        return options;
    }

    public static AzureSearchProviderStatus Enabled { get; } = new([]);

    public static AzureSearchProviderStatus Disabled { get; } = new(["AzureSearchProvider:Endpoint is missing."]);

    public static AzureSearchFieldOptions.Field Field(string name, AzureFieldValues values, bool facetable = false, bool sortable = false)
        => new() { PropertyName = name, FieldValues = values, Facetable = facetable, Sortable = sortable };

    public static Mock<IContentType> ContentType(string alias, Guid key, params (string Alias, string Editor)[] properties)
    {
        var contentType = new Mock<IContentType>();
        contentType.SetupGet(type => type.Alias).Returns(alias);
        contentType.SetupGet(type => type.Key).Returns(key);
        contentType.SetupGet(type => type.CompositionPropertyTypes).Returns(properties.Select(property =>
        {
            var propertyType = new Mock<IPropertyType>();
            propertyType.SetupGet(type => type.Alias).Returns(property.Alias);
            propertyType.SetupGet(type => type.PropertyEditorAlias).Returns(property.Editor);
            return propertyType.Object;
        }).ToArray());
        return contentType;
    }

    public static AzureSearchSchemaProvider SchemaProvider(
        AzureSearchOptions options,
        IEnumerable<IContentType>? contentTypes = null,
        AzureSearchFieldOptions.Field[]? globalFields = null,
        RuntimeLevel runtimeLevel = RuntimeLevel.Run)
    {
        var contentTypeService = new Mock<IContentTypeService>();
        foreach (IContentType contentType in contentTypes ?? [])
        {
            contentTypeService.Setup(service => service.Get(contentType.Alias)).Returns(contentType);
        }

        var runtimeState = new Mock<IRuntimeState>();
        runtimeState.SetupGet(state => state.Level).Returns(runtimeLevel);

        return new AzureSearchSchemaProvider(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new AzureSearchFieldOptions { Fields = globalFields ?? [] }),
            contentTypeService.Object,
            Mock.Of<IMediaTypeService>(),
            Mock.Of<IMemberTypeService>(),
            runtimeState.Object,
            NullLogger<AzureSearchSchemaProvider>.Instance);
    }

    public static IndexField[] SystemFields(Guid contentTypeKey, string name = "Item", string? culture = null)
    =>
    [
        new(CoreFieldNames.ContentTypeId, new IndexValue { Keywords = [contentTypeKey.ToString("D")] }, null, null),
        new(CoreFieldNames.Name, new IndexValue { TextsR1 = [name] }, culture, null),
    ];

    public static IOptions<T> Wrap<T>(T value)
        where T : class
        => Microsoft.Extensions.Options.Options.Create(value);
}
