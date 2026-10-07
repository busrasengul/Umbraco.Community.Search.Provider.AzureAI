using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;
using Umbraco.Community.Search.Provider.AzureAI.NotificationHandlers;
using Umbraco.Community.Search.Provider.AzureAI.Services;
using CoreIndexAliases = Umbraco.Cms.Search.Core.Constants.IndexAliases;

namespace Umbraco.Community.Search.Provider.AzureAI.DependencyInjection;

public static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds the Azure AI Search provider, configured from the <c>AzureSearchProvider</c> configuration section.
    /// </summary>
    /// <remarks>
    /// An invalid or missing configuration does not stop the site: the problems are logged on startup,
    /// no Azure indexes are registered and any Azure search returns no results.
    /// Call this after <c>AddExamineSearchProvider()</c> when <see cref="AzureSearchOptions.RegisterDefaultIndexes"/> is on.
    /// </remarks>
    public static IUmbracoBuilder AddAzureSearchProvider(this IUmbracoBuilder builder)
    {
        IConfigurationSection section = builder.Config.GetSection(AzureSearchOptions.SectionName);
        AzureSearchOptions options = section.Get<AzureSearchOptions>() ?? new AzureSearchOptions();
        AzureSearchFieldOptions globalFields = section.Get<AzureSearchFieldOptions>() ?? new AzureSearchFieldOptions();
        var status = new AzureSearchProviderStatus(AzureSearchOptionsValidator.Validate(options, globalFields.Fields));

        builder.Services.Configure<AzureSearchOptions>(section);
        builder.Services.Configure<AzureSearchFieldOptions>(section);
        builder.Services.AddSingleton(status);
        builder.Services.AddSingleton<IAzureSearchSchemaProvider, AzureSearchSchemaProvider>();
        builder.Services.AddSingleton<IAzureSearchIndexManager, AzureSearchIndexManager>();
        builder.Services.AddSingleton<IAzureSearchBatchWriter, AzureSearchBatchWriter>();
        builder.Services.AddSingleton<AzureSearchIndexInitializer>();
        builder.Services.AddTransient<IAzureSearchIndexer, AzureSearchIndexer>();
        builder.Services.AddTransient<IAzureSearchSearcher, AzureSearchSearcher>();

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, EnsureAzureSearchIndexesNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentTypeChangedNotification, ContentTypeChangedNotificationHandler>();
        builder.AddNotificationAsyncHandler<MediaTypeChangedNotification, ContentTypeChangedNotificationHandler>();
        builder.AddNotificationAsyncHandler<MemberTypeChangedNotification, ContentTypeChangedNotificationHandler>();

        if (status.IsEnabled is false)
        {
            return builder;
        }

        if (options.RegisterDefaultIndexes)
        {
            // last registration wins, so this replaces the default indexer and searcher
            builder.Services.AddTransient<IIndexer, AzureSearchIndexer>();
            builder.Services.AddTransient<ISearcher, AzureSearchSearcher>();
        }

        builder.Services.Configure<IndexOptions>(indexOptions =>
        {
            if (options.RegisterDefaultIndexes)
            {
                indexOptions.RegisterAzureSearchContentIndex<IDraftContentChangeStrategy>(CoreIndexAliases.DraftContent, UmbracoObjectTypes.Document);
                indexOptions.RegisterAzureSearchContentIndex<IPublishedContentChangeStrategy>(CoreIndexAliases.PublishedContent, UmbracoObjectTypes.Document);
                indexOptions.RegisterAzureSearchContentIndex<IDraftContentChangeStrategy>(CoreIndexAliases.DraftMedia, UmbracoObjectTypes.Media);
                indexOptions.RegisterAzureSearchContentIndex<IDraftContentChangeStrategy>(CoreIndexAliases.DraftMembers, UmbracoObjectTypes.Member);
            }

            foreach (AzureSearchIndexOptions index in options.Indexes)
            {
                if (index.ContentState is AzureIndexContentState.Published)
                {
                    indexOptions.RegisterAzureSearchContentIndex<IPublishedContentChangeStrategy>(index.Alias, index.ObjectTypes);
                }
                else
                {
                    indexOptions.RegisterAzureSearchContentIndex<IDraftContentChangeStrategy>(index.Alias, index.ObjectTypes);
                }
            }
        });

        return builder;
    }

    /// <summary>
    /// Registers a content index in code. Prefer <c>AzureSearchProvider:Indexes</c> in configuration unless you need a custom change strategy.
    /// </summary>
    public static IndexOptions RegisterAzureSearchContentIndex<TContentChangeStrategy>(
        this IndexOptions indexOptions,
        string indexAlias,
        params UmbracoObjectTypes[] containedObjectTypes)
        where TContentChangeStrategy : class, IContentChangeStrategy
    {
        indexOptions.RegisterContentIndex<IAzureSearchIndexer, IAzureSearchSearcher, TContentChangeStrategy>(
            indexAlias,
            sameOriginOnly: true,
            containedObjectTypes);
        return indexOptions;
    }
}
