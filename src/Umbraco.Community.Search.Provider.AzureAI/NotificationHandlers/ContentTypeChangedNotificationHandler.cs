using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.Services;

namespace Umbraco.Community.Search.Provider.AzureAI.NotificationHandlers;

/// <summary>
/// Re-resolves content type based indexes when a document, media or member type changes, so new properties become fields.
/// </summary>
internal sealed class ContentTypeChangedNotificationHandler :
    INotificationAsyncHandler<ContentTypeChangedNotification>,
    INotificationAsyncHandler<MediaTypeChangedNotification>,
    INotificationAsyncHandler<MemberTypeChangedNotification>
{
    private readonly IAzureSearchSchemaProvider _schemaProvider;
    private readonly AzureSearchIndexInitializer _initializer;
    private readonly AzureSearchProviderStatus _status;
    private readonly HashSet<string> _contentTypeIndexes;

    public ContentTypeChangedNotificationHandler(
        IAzureSearchSchemaProvider schemaProvider,
        AzureSearchIndexInitializer initializer,
        AzureSearchProviderStatus status,
        IOptions<AzureSearchOptions> options)
    {
        _schemaProvider = schemaProvider;
        _initializer = initializer;
        _status = status;
        _contentTypeIndexes = options.Value.Indexes
            .Where(index => index.ContentTypes.Length > 0)
            .Select(index => index.Alias)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Task HandleAsync(ContentTypeChangedNotification notification, CancellationToken cancellationToken) => RefreshAsync();

    public Task HandleAsync(MediaTypeChangedNotification notification, CancellationToken cancellationToken) => RefreshAsync();

    public Task HandleAsync(MemberTypeChangedNotification notification, CancellationToken cancellationToken) => RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_status.IsEnabled is false || _contentTypeIndexes.Count is 0)
        {
            return;
        }

        _schemaProvider.Reset();
        await _initializer.EnsureAsync(_contentTypeIndexes.Contains);
    }
}
