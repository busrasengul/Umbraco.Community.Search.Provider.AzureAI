using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Search.Provider.AzureAI.Services;

namespace Umbraco.Community.Search.Provider.AzureAI.NotificationHandlers;

/// <summary>
/// Creates or updates every Azure AI Search index on startup. Failures are logged, never thrown, so the site still boots.
/// </summary>
internal sealed class EnsureAzureSearchIndexesNotificationHandler : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly AzureSearchIndexInitializer _initializer;
    private readonly AzureSearchProviderStatus _status;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<EnsureAzureSearchIndexesNotificationHandler> _logger;

    public EnsureAzureSearchIndexesNotificationHandler(
        AzureSearchIndexInitializer initializer,
        AzureSearchProviderStatus status,
        IRuntimeState runtimeState,
        ILogger<EnsureAzureSearchIndexesNotificationHandler> logger)
    {
        _initializer = initializer;
        _status = status;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (_status.IsEnabled is false)
        {
            _logger.LogError("{Message}", _status.DisabledMessage);
            return;
        }

        if (_runtimeState.Level is not RuntimeLevel.Run)
        {
            return;
        }

        await _initializer.EnsureAsync(_ => true);
    }
}
