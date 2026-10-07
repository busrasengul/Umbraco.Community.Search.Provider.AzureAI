using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Aliases = Umbraco.Cms.Core.Constants.PropertyEditors.Aliases;

namespace Umbraco.Community.Search.Provider.AzureAI.Services;

/// <summary>
/// Maps a property editor to the field Umbraco Search indexes it as, following the core property value handlers.
/// Editors with mixed or no index values (rich text, blocks, media, labels) are not declared.
/// </summary>
internal static class AzureAutoFieldMapper
{
    public static AzureSearchFieldOptions.Field? Map(string propertyAlias, string propertyEditorAlias)
        => propertyEditorAlias switch
        {
            Aliases.TextBox or Aliases.TextArea or Aliases.PlainString or Aliases.MultipleTextstring
                => Field(propertyAlias, AzureFieldValues.Texts, facetable: false, sortable: false),

            Aliases.Integer or Aliases.PlainInteger or Aliases.Boolean
                => Field(propertyAlias, AzureFieldValues.Integers, facetable: true, sortable: true),

            Aliases.Decimal or Aliases.PlainDecimal
                => Field(propertyAlias, AzureFieldValues.Decimals, facetable: true, sortable: true),

            // a slider can hold a range, so it stays multi-valued and is not sortable
            Aliases.Slider
                => Field(propertyAlias, AzureFieldValues.Decimals, facetable: true, sortable: false),

            Aliases.DateTime or Aliases.PlainDateTime or Aliases.DateOnly or Aliases.TimeOnly
                or Aliases.DateTimeWithTimeZone or Aliases.DateTimeUnspecified
                => Field(propertyAlias, AzureFieldValues.DateTimeOffsets, facetable: true, sortable: true),

            Aliases.Tags or Aliases.DropDownListFlexible or Aliases.RadioButtonList or Aliases.CheckBoxList
                or Aliases.ContentPicker or Aliases.MultiNodeTreePicker
                => Field(propertyAlias, AzureFieldValues.Keywords, facetable: true, sortable: false),

            _ => null,
        };

    private static AzureSearchFieldOptions.Field Field(string propertyAlias, AzureFieldValues values, bool facetable, bool sortable)
        => new() { PropertyName = propertyAlias, FieldValues = values, Facetable = facetable, Sortable = sortable };
}
