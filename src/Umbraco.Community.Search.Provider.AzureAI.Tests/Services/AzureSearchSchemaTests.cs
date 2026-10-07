using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using static Umbraco.Community.Search.Provider.AzureAI.Tests.TestHelpers;
using CoreFieldNames = Umbraco.Cms.Search.Core.Constants.FieldNames;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Services;

public class AzureSearchSchemaTests
{
    [TestCase(CoreFieldNames.Id, "key", false)]
    [TestCase(CoreFieldNames.PathIds, "pathKeys", false)]
    [TestCase(CoreFieldNames.Name, "name", true)]
    [TestCase(CoreFieldNames.CreateDate, "createDate", true)]
    [TestCase(CoreFieldNames.UpdateDate, "updateDate", true)]
    public void Resolves_System_Fields(string fieldName, string azureName, bool sortable)
    {
        AzureField? field = AzureSearchSchema.Empty.Resolve(fieldName);

        Assert.That(field, Is.Not.Null);
        Assert.That(field!.Name, Is.EqualTo(azureName));
        Assert.That(field.Sortable, Is.EqualTo(sortable));
    }

    [Test]
    public void Undeclared_Fields_Do_Not_Resolve()
        => Assert.That(AzureSearchSchema.Empty.Resolve("price"), Is.Null);

    [Test]
    public void Sortable_Keyword_Uses_A_Separate_Sort_Field()
    {
        var schema = new AzureSearchSchema([Field("brand", AzureFieldValues.Keywords, facetable: true, sortable: true)]);

        AzureField field = schema.Resolve("Brand")!;

        Assert.Multiple(() =>
        {
            Assert.That(field.Name, Is.EqualTo("k_brand"));
            Assert.That(field.SortName, Is.EqualTo("k_brand_sort"));
            Assert.That(field.IsCollection, Is.True);
        });
    }

    [Test]
    public void Sortable_Number_Is_Single_Valued_And_Unsortable_Number_Is_A_Collection()
    {
        var schema = new AzureSearchSchema(
        [
            Field("price", AzureFieldValues.Decimals, sortable: true),
            Field("sizes", AzureFieldValues.Integers),
        ]);

        Assert.That(schema.Resolve("price")!.IsCollection, Is.False);
        Assert.That(schema.Resolve("sizes")!.IsCollection, Is.True);
    }

    [Test]
    public void Without_Content_Type_Restriction_Everything_Is_Accepted()
        => Assert.That(AzureSearchSchema.Empty.AcceptsContentType(SystemFields(Guid.NewGuid())), Is.True);

    [Test]
    public void With_Content_Type_Restriction_Only_Listed_Types_Are_Accepted()
    {
        var allowed = Guid.NewGuid();
        var schema = new AzureSearchSchema([], new HashSet<string> { allowed.ToString("D") });

        Assert.That(schema.AcceptsContentType(SystemFields(allowed)), Is.True);
        Assert.That(schema.AcceptsContentType(SystemFields(Guid.NewGuid())), Is.False);
        Assert.That(schema.AcceptsContentType(Array.Empty<IndexField>()), Is.False);
    }
}
