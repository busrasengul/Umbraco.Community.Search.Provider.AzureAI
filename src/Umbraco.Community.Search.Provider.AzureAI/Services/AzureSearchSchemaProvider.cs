using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;

namespace Umbraco.Community.Search.Provider.AzureAI.Services;

internal interface IAzureSearchSchemaProvider
{
    AzureSearchSchema GetSchema(string indexAlias);

    /// <summary>Forgets resolved schemas, e.g. after a content type changed.</summary>
    void Reset();
}

internal sealed class AzureSearchSchemaProvider : IAzureSearchSchemaProvider
{
    private readonly AzureSearchOptions _options;
    private readonly AzureSearchFieldOptions _globalFields;
    private readonly IContentTypeService _contentTypeService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly IMemberTypeService _memberTypeService;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<AzureSearchSchemaProvider> _logger;
    private readonly ConcurrentDictionary<string, AzureSearchSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);

    public AzureSearchSchemaProvider(
        IOptions<AzureSearchOptions> options,
        IOptions<AzureSearchFieldOptions> globalFields,
        IContentTypeService contentTypeService,
        IMediaTypeService mediaTypeService,
        IMemberTypeService memberTypeService,
        IRuntimeState runtimeState,
        ILogger<AzureSearchSchemaProvider> logger)
    {
        _options = options.Value;
        _globalFields = globalFields.Value;
        _contentTypeService = contentTypeService;
        _mediaTypeService = mediaTypeService;
        _memberTypeService = memberTypeService;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public AzureSearchSchema GetSchema(string indexAlias)
    {
        if (_schemas.TryGetValue(indexAlias, out AzureSearchSchema? schema))
        {
            return schema;
        }

        // content types can only be read once Umbraco is installed and running
        if (_runtimeState.Level is not RuntimeLevel.Run)
        {
            return Build(indexAlias, contentTypesAvailable: false);
        }

        return _schemas.GetOrAdd(indexAlias, alias => Build(alias, contentTypesAvailable: true));
    }

    public void Reset() => _schemas.Clear();

    private AzureSearchSchema Build(string indexAlias, bool contentTypesAvailable)
    {
        AzureSearchIndexOptions? index = _options.Indexes
            .FirstOrDefault(candidate => string.Equals(candidate.Alias, indexAlias, StringComparison.OrdinalIgnoreCase));

        var fields = new Dictionary<string, AzureSearchFieldOptions.Field>(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? contentTypeKeys = null;

        if (index is { ContentTypes.Length: > 0 })
        {
            // Until the content types can be read (e.g. while Umbraco installs), accept nothing rather than everything.
            // That schema is not cached, and a rebuild indexes anything skipped meanwhile.
            contentTypeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (contentTypesAvailable)
            {
                IContentTypeComposition[] contentTypes = ResolveContentTypes(index);
                contentTypeKeys.UnionWith(contentTypes.Select(contentType => contentType.Key.ToString("D")));

                if (index.AutoFields)
                {
                    foreach (AzureSearchFieldOptions.Field field in AutoFields(indexAlias, contentTypes))
                    {
                        fields[field.PropertyName] = field;
                    }
                }
            }
        }

        // explicit declarations win: global fields first, then the index's own
        foreach (AzureSearchFieldOptions.Field field in _globalFields.Fields.Concat(index?.Fields ?? []))
        {
            fields[field.PropertyName] = field;
        }

        return new AzureSearchSchema(fields.Values, contentTypeKeys);
    }

    private IContentTypeComposition[] ResolveContentTypes(AzureSearchIndexOptions index)
    {
        var resolved = new List<IContentTypeComposition>();
        foreach (var alias in index.ContentTypes)
        {
            IContentTypeComposition? contentType = index.ObjectTypes
                .Select(objectType => Find(objectType, alias))
                .FirstOrDefault(found => found is not null);

            if (contentType is null)
            {
                _logger.LogWarning(
                    "Azure AI Search index {IndexAlias} lists the content type {ContentTypeAlias}, which does not exist for {ObjectTypes}. It is ignored.",
                    index.Alias,
                    alias,
                    string.Join(", ", index.ObjectTypes));
                continue;
            }

            resolved.Add(contentType);
        }

        return resolved.ToArray();
    }

    private IContentTypeComposition? Find(UmbracoObjectTypes objectType, string alias)
        => objectType switch
        {
            UmbracoObjectTypes.Document => _contentTypeService.Get(alias),
            UmbracoObjectTypes.Media => _mediaTypeService.Get(alias),
            UmbracoObjectTypes.Member => _memberTypeService.Get(alias),
            _ => null,
        };

    private IEnumerable<AzureSearchFieldOptions.Field> AutoFields(string indexAlias, IEnumerable<IContentTypeComposition> contentTypes)
    {
        var fields = new Dictionary<string, AzureSearchFieldOptions.Field>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (IPropertyType propertyType in contentTypes.SelectMany(contentType => contentType.CompositionPropertyTypes))
        {
            AzureSearchFieldOptions.Field? field = AzureAutoFieldMapper.Map(propertyType.Alias, propertyType.PropertyEditorAlias);
            if (field is null || conflicts.Contains(field.PropertyName))
            {
                continue;
            }

            if (fields.TryGetValue(field.PropertyName, out AzureSearchFieldOptions.Field? existing) && existing.FieldValues != field.FieldValues)
            {
                _logger.LogWarning(
                    "Azure AI Search index {IndexAlias} cannot declare {PropertyAlias} automatically: it is {First} on one content type and {Second} on another. Declare it under Fields to choose.",
                    indexAlias,
                    field.PropertyName,
                    existing.FieldValues,
                    field.FieldValues);
                fields.Remove(field.PropertyName);
                conflicts.Add(field.PropertyName);
                continue;
            }

            fields[field.PropertyName] = field;
        }

        return fields.Values;
    }
}
