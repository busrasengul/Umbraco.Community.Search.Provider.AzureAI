using Azure.Search.Documents.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using CoreFieldNames = Umbraco.Cms.Search.Core.Constants.FieldNames;

namespace Umbraco.Community.Search.Provider.AzureAI.Services;

/// <summary>
/// Builds one Azure document per culture from the fields Umbraco Search collected for a content item.
/// </summary>
internal static class AzureSearchDocumentBuilder
{
    public static SearchDocument[] Build(
        AzureSearchSchema schema,
        Guid id,
        UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations,
        IndexField[] fields,
        ContentProtection? protection)
    {
        var accessKeys = protection?.AccessIds.Select(accessId => accessId.ToString("D")).ToArray() ?? [];

        return variations
            .Where(variation => variation.Segment is null)
            .Select(variation => variation.Culture)
            .Distinct()
            .Select(culture => Build(schema, id, objectType, culture, fields, accessKeys))
            .ToArray();
    }

    private static SearchDocument Build(AzureSearchSchema schema, Guid id, UmbracoObjectTypes objectType, string? culture, IndexField[] fields, string[] accessKeys)
    {
        var document = new SearchDocument
        {
            [AzureFieldNames.Id] = $"{id:N}-{culture?.ToLowerInvariant() ?? AzureFieldNames.InvariantCulture}",
            [AzureFieldNames.Key] = id.ToString("D"),
            [AzureFieldNames.ObjectType] = objectType.ToString(),
            [AzureFieldNames.Culture] = culture?.ToLowerInvariant() ?? AzureFieldNames.InvariantCulture,
            [AzureFieldNames.AccessKeys] = accessKeys,
        };

        var textsR1 = new List<string>();
        var textsR2 = new List<string>();
        var textsR3 = new List<string>();
        var texts = new List<string>();

        IEnumerable<IndexField> applicableFields = fields.Where(field =>
            field.Segment is null
            && (field.Culture is null || string.Equals(field.Culture, culture, StringComparison.OrdinalIgnoreCase)));

        foreach (IndexField field in applicableFields)
        {
            IndexValue value = field.Value;
            textsR1.AddRange(value.TextsR1 ?? []);
            textsR2.AddRange(value.TextsR2 ?? []);
            textsR3.AddRange(value.TextsR3 ?? []);
            texts.AddRange(value.Texts ?? []);

            switch (field.FieldName)
            {
                case CoreFieldNames.PathIds:
                    document[AzureFieldNames.PathKeys] = value.Keywords?.ToArray() ?? [];
                    break;
                case CoreFieldNames.ContentTypeId:
                    document[AzureFieldNames.ContentTypeId] = value.Keywords?.FirstOrDefault();
                    break;
                case CoreFieldNames.Name:
                    document[AzureFieldNames.Name] = (value.TextsR1 ?? value.Texts ?? value.Keywords)?.FirstOrDefault();
                    break;
                case CoreFieldNames.CreateDate:
                    document[AzureFieldNames.CreateDate] = value.DateTimeOffsets?.FirstOrDefault();
                    break;
                case CoreFieldNames.UpdateDate:
                    document[AzureFieldNames.UpdateDate] = value.DateTimeOffsets?.FirstOrDefault();
                    break;
                default:
                    if (schema.TryGetDeclaredField(field.FieldName, out AzureSearchFieldOptions.Field declaredField))
                    {
                        AddDeclaredValue(document, declaredField, value);
                    }

                    break;
            }
        }

        document[AzureFieldNames.TextsR1] = textsR1;
        document[AzureFieldNames.TextsR2] = textsR2;
        document[AzureFieldNames.TextsR3] = textsR3;
        document[AzureFieldNames.Texts] = texts;

        return document;
    }

    private static void AddDeclaredValue(SearchDocument document, AzureSearchFieldOptions.Field field, IndexValue value)
    {
        var name = AzureFieldNames.ForProperty(field);

        switch (field.FieldValues)
        {
            case AzureFieldValues.Keywords:
                var keywords = value.Keywords?.ToArray() ?? [];
                document[name] = keywords;
                if (field.Sortable)
                {
                    document[AzureFieldNames.SortForKeyword(field)] = keywords.FirstOrDefault();
                }

                break;
            case AzureFieldValues.Integers:
                SetNumeric(document, name, field.Sortable, value.Integers?.ToArray() ?? []);
                break;
            case AzureFieldValues.Decimals:
                SetNumeric(document, name, field.Sortable, value.Decimals?.Select(d => (double)d).ToArray() ?? []);
                break;
            case AzureFieldValues.DateTimeOffsets:
                SetNumeric(document, name, field.Sortable, value.DateTimeOffsets?.ToArray() ?? []);
                break;
            case AzureFieldValues.Texts:
                document[name] = (value.Texts ?? [])
                    .Concat(value.TextsR1 ?? [])
                    .Concat(value.TextsR2 ?? [])
                    .Concat(value.TextsR3 ?? [])
                    .ToArray();
                break;
        }
    }

    private static void SetNumeric<T>(SearchDocument document, string name, bool single, T[] values)
        where T : struct
        => document[name] = single
            ? values.Length > 0 ? values[0] : null
            : values;
}
