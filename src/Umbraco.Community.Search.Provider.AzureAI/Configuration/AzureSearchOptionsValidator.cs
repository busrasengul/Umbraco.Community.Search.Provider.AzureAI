using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.Search.Provider.AzureAI.Configuration;

/// <summary>
/// Checks the provider configuration up front, so problems are reported once at startup with the setting that caused them.
/// </summary>
public static partial class AzureSearchOptionsValidator
{
    private const string Section = AzureSearchOptions.SectionName;

    private static readonly UmbracoObjectTypes[] SupportedObjectTypes =
        [UmbracoObjectTypes.Document, UmbracoObjectTypes.Media, UmbracoObjectTypes.Member];

    public static IReadOnlyList<string> Validate(AzureSearchOptions options, IEnumerable<AzureSearchFieldOptions.Field>? globalFields = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            errors.Add($"{Section}:Endpoint is missing.");
        }
        else if (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? endpoint) is false || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"{Section}:Endpoint '{options.Endpoint}' must be an absolute https URL, e.g. https://my-service.search.windows.net.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add($"{Section}:ApiKey is missing.");
        }

        if (options.IndexPrefix.Length > 0 && IndexPrefixPattern().IsMatch(options.IndexPrefix.ToLowerInvariant().Replace('_', '-')) is false)
        {
            errors.Add($"{Section}:IndexPrefix '{options.IndexPrefix}' may only contain letters, digits, dashes and underscores, and must start with a letter or digit.");
        }

        Positive(errors, options.MaxFacetValues, "MaxFacetValues");
        Positive(errors, options.BatchSize, "BatchSize");
        Positive(errors, options.MaxIndexingAttempts, "MaxIndexingAttempts");
        if (options.FlushDelayMilliseconds < 0)
        {
            errors.Add($"{Section}:FlushDelayMilliseconds must be zero or more.");
        }

        ValidateFields(errors, globalFields ?? [], $"{Section}:Fields");

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < options.Indexes.Length; i++)
        {
            AzureSearchIndexOptions index = options.Indexes[i];
            var path = $"{Section}:Indexes:{i}";

            if (string.IsNullOrWhiteSpace(index.Alias))
            {
                errors.Add($"{path}:Alias is missing.");
            }
            else
            {
                if (aliases.Add(index.Alias) is false)
                {
                    errors.Add($"{path}:Alias '{index.Alias}' is declared more than once.");
                }

                var indexName = $"{options.IndexPrefix}{index.Alias}".ToLowerInvariant().Replace('_', '-');
                if (IndexNamePattern().IsMatch(indexName) is false || indexName.Length > 128)
                {
                    errors.Add($"{path}:Alias '{index.Alias}' gives the Azure index name '{indexName}', which must be at most 128 lowercase letters, digits or dashes, starting and ending with a letter or digit.");
                }
            }

            if (index.ObjectTypes.Length is 0)
            {
                errors.Add($"{path}:ObjectTypes must contain at least one of Document, Media or Member.");
            }

            foreach (UmbracoObjectTypes objectType in index.ObjectTypes.Where(objectType => SupportedObjectTypes.Contains(objectType) is false))
            {
                errors.Add($"{path}:ObjectTypes contains '{objectType}'. Only Document, Media and Member can be indexed.");
            }

            if (index.ContentState is AzureIndexContentState.Published && index.ObjectTypes.Any(objectType => objectType is not UmbracoObjectTypes.Document))
            {
                errors.Add($"{path}:ContentState must be Draft when the index contains media or members, as they are not published.");
            }

            if (index.AutoFields && index.ContentTypes.Length is 0)
            {
                errors.Add($"{path}:AutoFields needs ContentTypes, so the provider knows which properties to declare.");
            }

            ValidateFields(errors, index.Fields, $"{path}:Fields");
        }

        return errors;
    }

    private static void ValidateFields(List<string> errors, IEnumerable<AzureSearchFieldOptions.Field> fields, string path)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        foreach (AzureSearchFieldOptions.Field field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.PropertyName))
            {
                errors.Add($"{path}:{i}:PropertyName is missing.");
            }
            else if (FieldNamePattern().IsMatch(field.PropertyName) is false)
            {
                errors.Add($"{path}:{i}:PropertyName '{field.PropertyName}' may only contain letters, digits and underscores, and must start with a letter.");
            }
            else if (names.Add(field.PropertyName) is false)
            {
                errors.Add($"{path}:{i}:PropertyName '{field.PropertyName}' is declared more than once.");
            }

            if (field.FieldValues is AzureFieldValues.Texts && (field.Facetable || field.Sortable))
            {
                errors.Add($"{path}:{i} ('{field.PropertyName}') is Texts, which can be neither facetable nor sortable. Use Keywords instead.");
            }

            i++;
        }
    }

    private static void Positive(List<string> errors, int value, string name)
    {
        if (value <= 0)
        {
            errors.Add($"{Section}:{name} must be greater than zero.");
        }
    }

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]*[a-z0-9])?$")]
    private static partial Regex IndexNamePattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    private static partial Regex IndexPrefixPattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex FieldNamePattern();
}
