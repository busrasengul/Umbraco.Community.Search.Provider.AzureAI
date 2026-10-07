using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using Aliases = Umbraco.Cms.Core.Constants.PropertyEditors.Aliases;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Services;

public class AzureAutoFieldMapperTests
{
    [TestCase(Aliases.TextBox, AzureFieldValues.Texts, false, false)]
    [TestCase(Aliases.TextArea, AzureFieldValues.Texts, false, false)]
    [TestCase(Aliases.MultipleTextstring, AzureFieldValues.Texts, false, false)]
    [TestCase(Aliases.Integer, AzureFieldValues.Integers, true, true)]
    [TestCase(Aliases.Boolean, AzureFieldValues.Integers, true, true)]
    [TestCase(Aliases.Decimal, AzureFieldValues.Decimals, true, true)]
    [TestCase(Aliases.Slider, AzureFieldValues.Decimals, true, false)]
    [TestCase(Aliases.DateTime, AzureFieldValues.DateTimeOffsets, true, true)]
    [TestCase(Aliases.DateOnly, AzureFieldValues.DateTimeOffsets, true, true)]
    [TestCase(Aliases.Tags, AzureFieldValues.Keywords, true, false)]
    [TestCase(Aliases.DropDownListFlexible, AzureFieldValues.Keywords, true, false)]
    [TestCase(Aliases.RadioButtonList, AzureFieldValues.Keywords, true, false)]
    [TestCase(Aliases.CheckBoxList, AzureFieldValues.Keywords, true, false)]
    [TestCase(Aliases.ContentPicker, AzureFieldValues.Keywords, true, false)]
    [TestCase(Aliases.MultiNodeTreePicker, AzureFieldValues.Keywords, true, false)]
    public void Maps_Editor_To_The_Value_Type_Umbraco_Search_Indexes(string editorAlias, AzureFieldValues values, bool facetable, bool sortable)
    {
        AzureSearchFieldOptions.Field? field = AzureAutoFieldMapper.Map("myProperty", editorAlias);

        Assert.That(field, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(field!.PropertyName, Is.EqualTo("myProperty"));
            Assert.That(field.FieldValues, Is.EqualTo(values));
            Assert.That(field.Facetable, Is.EqualTo(facetable));
            Assert.That(field.Sortable, Is.EqualTo(sortable));
        });
    }

    [TestCase(Aliases.RichText)]
    [TestCase(Aliases.BlockList)]
    [TestCase(Aliases.BlockGrid)]
    [TestCase(Aliases.MediaPicker3)]
    [TestCase(Aliases.Label)]
    [TestCase("My.Custom.Editor")]
    public void Skips_Editors_Without_A_Single_Value_Type(string editorAlias)
        => Assert.That(AzureAutoFieldMapper.Map("myProperty", editorAlias), Is.Null);
}
