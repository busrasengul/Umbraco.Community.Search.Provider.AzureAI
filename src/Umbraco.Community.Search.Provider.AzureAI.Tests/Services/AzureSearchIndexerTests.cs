using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using static Umbraco.Community.Search.Provider.AzureAI.Tests.TestHelpers;

namespace Umbraco.Community.Search.Provider.AzureAI.Tests.Services;

public class AzureSearchIndexerTests
{
    private readonly Guid _allowedType = Guid.NewGuid();
    private Mock<IAzureSearchBatchWriter> _batchWriter = null!;
    private Mock<IAzureSearchIndexManager> _indexManager = null!;

    [SetUp]
    public void SetUp()
    {
        _batchWriter = new Mock<IAzureSearchBatchWriter>();
        _indexManager = new Mock<IAzureSearchIndexManager>();
    }

    private AzureSearchIndexer Indexer(AzureSearchProviderStatus status)
    {
        var schemaProvider = new Mock<IAzureSearchSchemaProvider>();
        schemaProvider
            .Setup(provider => provider.GetSchema(It.IsAny<string>()))
            .Returns(new AzureSearchSchema([], new HashSet<string> { _allowedType.ToString("D") }));

        return new AzureSearchIndexer(_indexManager.Object, schemaProvider.Object, _batchWriter.Object, status, NullLogger<AzureSearchIndexer>.Instance);
    }

    [Test]
    public async Task Allowed_Content_Is_Queued_For_Upload()
    {
        await Indexer(Enabled).AddOrUpdateAsync("Products", Guid.NewGuid(), UmbracoObjectTypes.Document, [new(null, null)], SystemFields(_allowedType), null);

        _batchWriter.Verify(writer => writer.EnqueueAsync("Products", It.Is<IEnumerable<SearchDocument>>(documents => documents.Count() == 1)), Times.Once);
    }

    [Test]
    public async Task Content_Of_Other_Types_Is_Skipped()
    {
        await Indexer(Enabled).AddOrUpdateAsync("Products", Guid.NewGuid(), UmbracoObjectTypes.Document, [new(null, null)], SystemFields(Guid.NewGuid()), null);

        _batchWriter.Verify(writer => writer.EnqueueAsync(It.IsAny<string>(), It.IsAny<IEnumerable<SearchDocument>>()), Times.Never);
    }

    [Test]
    public async Task Nothing_Happens_When_Disabled()
    {
        AzureSearchIndexer indexer = Indexer(Disabled);

        await indexer.AddOrUpdateAsync("Products", Guid.NewGuid(), UmbracoObjectTypes.Document, [new(null, null)], SystemFields(_allowedType), null);
        await indexer.DeleteAsync("Products", [Guid.NewGuid()]);
        await indexer.ResetAsync("Products");
        IndexMetadata metadata = await indexer.GetMetadataAsync("Products");

        Assert.That(metadata.HealthStatus, Is.EqualTo(HealthStatus.Unknown));
        _batchWriter.VerifyNoOtherCalls();
        _indexManager.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Reset_Discards_Pending_Documents_And_Recreates_The_Index()
    {
        await Indexer(Enabled).ResetAsync("Products");

        _batchWriter.Verify(writer => writer.Discard("Products"), Times.Once);
        _indexManager.Verify(manager => manager.ResetAsync("Products"), Times.Once);
    }
}
