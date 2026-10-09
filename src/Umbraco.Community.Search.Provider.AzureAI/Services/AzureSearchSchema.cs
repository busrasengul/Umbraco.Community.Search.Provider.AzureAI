using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using CoreFieldNames = Umbraco.Cms.Search.Core.Constants.FieldNames;

namespace Umbraco.Community.Search.Provider.AzureAI.Services;

internal sealed record AzureField(string Name, AzureFieldValues Values, bool IsCollection, string SortName, bool Sortable);

/// <summary>
/// The declared fields and content type restriction of a single index.
/// </summary>
internal sealed class AzureSearchSchema
{
    private readonly Dictionary<string, AzureSearchFieldOptions.Field> _declaredFields;

    public AzureSearchSchema(IEnumerable<AzureSearchFieldOptions.Field> fields, IReadOnlySet<string>? contentTypeKeys = null)
    {
        _declaredFields = new Dictionary<string, AzureSearchFieldOptions.Field>(StringComparer.OrdinalIgnoreCase);
        foreach (AzureSearchFieldOptions.Field field in fields)
        {
            _declaredFields[field.PropertyName] = field;
        }

        ContentTypeKeys = contentTypeKeys;
    }

    public static AzureSearchSchema Empty { get; } = new([]);

    public IEnumerable<AzureSearchFieldOptions.Field> DeclaredFields => _declaredFields.Values;

    /// <summary>Content type keys (<c>D</c> format) the index accepts; <c>null</c> accepts every content type.</summary>
    public IReadOnlySet<string>? ContentTypeKeys { get; }

    public bool TryGetDeclaredField(string? propertyName, out AzureSearchFieldOptions.Field field)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            field = null!;
            return false;
        }

        return _declaredFields.TryGetValue(propertyName, out field!);
    }

    public bool AcceptsContentType(IEnumerable<IndexField> fields)
    {
        if (ContentTypeKeys is null)
        {
            return true;
        }

        var contentTypeKey = fields
            .FirstOrDefault(field => field.FieldName == CoreFieldNames.ContentTypeId)?
            .Value.Keywords?
            .FirstOrDefault();

        return contentTypeKey is not null && ContentTypeKeys.Contains(contentTypeKey);
    }

    public AzureField? Resolve(string fieldName)
        => fieldName switch
        {
            CoreFieldNames.Id => new(AzureFieldNames.Key, AzureFieldValues.Keywords, false, AzureFieldNames.Key, false),
            CoreFieldNames.ContentTypeId => new(AzureFieldNames.ContentTypeId, AzureFieldValues.Keywords, false, AzureFieldNames.ContentTypeId, false),
            CoreFieldNames.ObjectType => new(AzureFieldNames.ObjectType, AzureFieldValues.Keywords, false, AzureFieldNames.ObjectType, false),
            CoreFieldNames.PathIds => new(AzureFieldNames.PathKeys, AzureFieldValues.Keywords, true, AzureFieldNames.PathKeys, false),
            CoreFieldNames.Name => new(AzureFieldNames.Name, AzureFieldValues.Keywords, false, AzureFieldNames.Name, true),
            CoreFieldNames.CreateDate => new(AzureFieldNames.CreateDate, AzureFieldValues.DateTimeOffsets, false, AzureFieldNames.CreateDate, true),
            CoreFieldNames.UpdateDate => new(AzureFieldNames.UpdateDate, AzureFieldValues.DateTimeOffsets, false, AzureFieldNames.UpdateDate, true),
            _ => TryGetDeclaredField(fieldName, out AzureSearchFieldOptions.Field field)
                ? new(AzureFieldNames.ForProperty(field), field.FieldValues, IsCollection(field), SortName(field), field.Sortable)
                : null,
        };

    public static bool IsCollection(AzureSearchFieldOptions.Field field)
        => field.FieldValues is AzureFieldValues.Keywords or AzureFieldValues.Texts || field.Sortable is false;

    private static string SortName(AzureSearchFieldOptions.Field field)
        => field.FieldValues is AzureFieldValues.Keywords
            ? AzureFieldNames.SortForKeyword(field)
            : AzureFieldNames.ForProperty(field);
}
