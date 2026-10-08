# Umbraco.Community.Search.Provider.AzureAI

[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.Search.Provider.AzureAI?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.Search.Provider.AzureAI)
[![GitHub license](https://img.shields.io/github/license/busrasengul/Umbraco.Community.Search.Provider.AzureAI?color=8AB803)](../LICENSE)

<img src="../docs/icon.png" alt="Logo" width="128" align="right" />

An [Azure AI Search](https://learn.microsoft.com/azure/search/) provider for [Umbraco Search](https://github.com/umbraco/Umbraco.Cms.Search).

Umbraco keeps your Azure indexes up to date as content is published, moved, unpublished or deleted.
You query them through the standard Umbraco Search `ISearcher`, with:

- full text search, with relevance boosting for names and headings, and optional fuzzy matching
- filters, facets (including range facets) and sorting
- cultures and member-protected content
- indexes declared in `appsettings.json`, limited to the content types you choose
- fields declared automatically from your content types' property editors

Your search code stays provider-agnostic. Moving between Examine, Algolia and Azure AI Search is a configuration change.

- [Versions](#versions)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Fields](#fields)
- [Searching](#searching)
- [Rebuilding indexes](#rebuilding-indexes)
- [Error handling](#error-handling)
- [Troubleshooting](#troubleshooting)
- [Test site](#test-site)
- [Contributing](#contributing)

## Versions

| Package | Umbraco | Umbraco Search | .NET | Branch |
|---|---|---|---|---|
| 17.x | 17 | 17.2+ | 10 | `v17` |
| 18.x | 18 | 18.2+ | 10 | `v18` |
| 19.0.0-beta1 | 19 (pre-release) | 19.x | 11 | `v19` |

NuGet picks the right version for your Umbraco major.

## Getting started

### 1. Create an Azure AI Search service

In the Azure portal, create an **Azure AI Search** service. From it you need:

- the **URL** on the *Overview* page, e.g. `https://my-service.search.windows.net`
- an **admin key** from *Settings → Keys*. Query keys cannot create indexes or upload documents.

Check how many indexes your tier allows: every index you configure is a separate Azure index.

### 2. Install the package

```bash
dotnet add package Umbraco.Community.Search.Provider.AzureAI
```

### 3. Add the provider

Register Umbraco Search and the provider in a composer:

```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Community.Search.Provider.AzureAI.DependencyInjection;

public class SearchComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder
            .AddSearchCore()
            .AddAzureSearchProvider();
}
```

If you also use the Examine provider, call `AddAzureSearchProvider()` after `AddExamineSearchProvider()`.

### 4. Configure it

Add the endpoint and your indexes to `appsettings.json`:

```json
{
  "AzureSearchProvider": {
    "Endpoint": "https://my-service.search.windows.net",
    "Indexes": [
      {
        "Alias": "Products",
        "ContentTypes": [ "product" ],
        "AutoFields": true
      }
    ]
  }
}
```

Keep the API key out of source control. Locally, use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets set "AzureSearchProvider:ApiKey" "<admin key>"
```

In production, use an environment variable (`AzureSearchProvider__ApiKey`) or Azure Key Vault.

### 5. Start the site

On startup the provider creates (or updates) an Azure index for every configured index, with its fields.
From then on, everything you publish is indexed automatically.

> [!NOTE]
> Umbraco Search processes indexing on a background queue that starts about a minute after the site boots.
> Changes made in the first minute are indexed once the queue starts.

### 6. Index your existing content

New indexes start empty. If the site already has content, rebuild each index once.
See [Rebuilding indexes](#rebuilding-indexes).

## Configuration

All settings live under `AzureSearchProvider`.

| Setting | Default | Description |
|---|---|---|
| `Endpoint` | | The search service URL. Must be `https`. |
| `ApiKey` | | An **admin** key for the search service. |
| `IndexPrefix` | `""` | Prefixed to every Azure index name, so several sites or environments can share one service, e.g. `dev-`. |
| `Indexes` | `[]` | The indexes to create and keep up to date. See below. |
| `Fields` | `[]` | Fields declared for every Azure index. See [Fields](#fields). |
| `RegisterDefaultIndexes` | `false` | Moves the four default Umbraco Search indexes (published content, draft content, media and members) to Azure. That makes backoffice search and Delivery API search use Azure, and needs room for four more indexes on your tier. |
| `FuzzySearch` | `true` | Allows one typo in query terms of four characters or more. |
| `MaxFacetValues` | `100` | The most values returned for a keyword facet. |
| `BatchSize` | `250` | Documents uploaded per request. |
| `FlushDelayMilliseconds` | `1000` | How long changes are collected before a partial batch is uploaded. |
| `MaxIndexingAttempts` | `8` | Upload attempts when Azure throttles (HTTP 429/503), with exponential backoff. |

### Indexes

| Setting | Default | Description |
|---|---|---|
| `Alias` | | The index alias you search with. |
| `ObjectTypes` | `["Document"]` | Any of `Document`, `Media` and `Member`. |
| `ContentState` | `Published` | `Published` or `Draft`. Media and member indexes must use `Draft`. |
| `ContentTypes` | `[]` | Content type aliases to include. Empty includes every content type. |
| `AutoFields` | `false` | Declares fields from the properties of `ContentTypes`. See [Automatic fields](#automatic-fields). |
| `Fields` | `[]` | Fields declared for this index only. These win over automatic and global fields. |

A fuller example, with a products index and a media index:

```json
{
  "AzureSearchProvider": {
    "Endpoint": "https://my-service.search.windows.net",
    "IndexPrefix": "prod-",
    "Indexes": [
      {
        "Alias": "Products",
        "ContentTypes": [ "product", "productVariant" ],
        "AutoFields": true,
        "Fields": [
          { "PropertyName": "price", "FieldValues": "Decimals", "Facetable": true, "Sortable": true }
        ]
      },
      {
        "Alias": "Downloads",
        "ObjectTypes": [ "Media" ],
        "ContentState": "Draft",
        "ContentTypes": [ "File" ]
      }
    ]
  }
}
```

### Index names

The Azure index name is the prefix plus the alias, in lowercase, with underscores turned into dashes.
With the example above, `Products` becomes `prod-products`.

## Fields

Every property is **full text searchable** without any setup.
To **filter, facet or sort** on a property, it must be declared as a field, either automatically or explicitly.

### Automatic fields

With `AutoFields`, the provider reads the index's content types (including compositions) and declares a field for each property, based on its editor:

| Property editor | Field type | Facetable | Sortable |
|---|---|---|---|
| Textstring, Textarea, Repeatable textstrings | `Texts` | | |
| Numeric, True/false | `Integers` | ✓ | ✓ |
| Decimal | `Decimals` | ✓ | ✓ |
| Slider | `Decimals` | ✓ | |
| Date and time pickers | `DateTimeOffsets` | ✓ | ✓ |
| Tags, Dropdown, Radio button list, Checkbox list, Content picker, Multinode tree picker | `Keywords` | ✓ | |

The mapping follows how Umbraco Search indexes each editor.
Rich text, block editors, media pickers and labels are still searchable, but they are not declared because they do not have a single value type.

When you change one of the content types, the fields are refreshed and new ones are added to the Azure index straight away.

If the same property alias has different types on two content types (say, `price` is Decimal on one and Integer on another), it is skipped and a warning is logged. Declare it explicitly to choose.

### Explicit fields

Declare fields under an index's `Fields`, or under the top-level `Fields` to apply them to every index:

```json
"Fields": [
  { "PropertyName": "category", "FieldValues": "Keywords", "Facetable": true },
  { "PropertyName": "price", "FieldValues": "Decimals", "Facetable": true, "Sortable": true },
  { "PropertyName": "publishDate", "FieldValues": "DateTimeOffsets", "Sortable": true }
]
```

`FieldValues` must match how Umbraco Search indexes the property: `Keywords`, `Integers`, `Decimals`, `DateTimeOffsets` or `Texts`.

- A **sortable** number or date holds one value per document, so only mark single-value properties as sortable.
- `Texts` fields can be filtered with a text filter, but cannot be facetable or sortable. Use `Keywords` for that.

> [!IMPORTANT]
> Adding a field updates the Azure index in place. **Changing the type of an existing field requires a rebuild**, which recreates the Azure index.

## Searching

Resolve the searcher for your index through Umbraco Search, and query it:

```csharp
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
using Umbraco.Cms.Search.Core.Models.Searching.Sorting;
using Umbraco.Cms.Search.Core.Services;

public class ProductSearchService(ISearcherResolver searcherResolver, IPublishedContentCache contentCache)
{
    public async Task<IEnumerable<IPublishedContent>> SearchAsync(string? query, string[] brands)
    {
        ISearcher searcher = searcherResolver.GetRequiredSearcher("Products");

        SearchResult result = await searcher.SearchAsync(
            "Products",
            query: query,
            filters: brands.Length > 0 ? [new KeywordFilter("brand", brands, false)] : [],
            facets:
            [
                new KeywordFacet("brand"),
                new DecimalRangeFacet("price", [new("budget", null, 50m), new("premium", 50m, null)]),
            ],
            sorters: [new DecimalSorter("price", Direction.Ascending)],
            culture: "en-US",
            skip: 0,
            take: 20);

        // result.Total is the number of matches, result.Facets holds the facet counts
        return result.Documents
            .Select(document => contentCache.GetById(document.Id))
            .OfType<IPublishedContent>();
    }
}
```

The provider only answers searches: building the search page is up to your site, the same as with any Umbraco Search provider.

A few things to know:

- Facets on a field you are filtering by are counted as if that filter was not applied, so visitors can still see and pick the other values.
- Filters, facets and sorters on undeclared fields are ignored, with a warning in the log.
- Anonymous searches exclude member-protected content. Pass an `AccessContext` for logged-in members.

## Rebuilding indexes

A rebuild recreates the Azure index and fills it with all matching content. You need one:

- when you add the provider to a site that already has content
- after changing the type of an existing field
- after deleting an index in the Azure portal

Trigger it with `IDistributedContentIndexRebuilder`. The rebuild runs in the background.

A simple approach is to rebuild empty indexes when the site starts:

```csharp
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Search.Core.Extensions;
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;

public class RebuildEmptyIndexes(
    IIndexerResolver indexerResolver,
    IDistributedContentIndexRebuilder rebuilder,
    ILogger<RebuildEmptyIndexes> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly string[] Indexes = ["Products"];

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var index in Indexes)
        {
            IndexMetadata metadata = await indexerResolver.GetRequiredIndexer(index).GetMetadataAsync(index);
            if (metadata.DocumentCount is 0)
            {
                logger.LogInformation("Index {Index} is empty, rebuilding", index);
                rebuilder.Rebuild(index);
            }
        }
    }
}

public class RebuildEmptyIndexesComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RebuildEmptyIndexes>();
}
```

You can call `rebuilder.Rebuild("Products")` from anywhere else too, for example a backoffice action. If you expose it over HTTP, make sure only administrators can reach it.

`GetMetadataAsync` also tells you how far a rebuild has got: `DocumentCount` grows as content is indexed.

## Error handling

The provider is built so that search problems never take your site down:

- **Missing or invalid configuration:** every problem is logged once on startup, naming the setting that caused it, e.g. `AzureSearchProvider:Indexes:0:ContentState must be Draft when the index contains media or members`. The provider then switches itself off: no Azure indexes are registered, indexing is skipped and Azure searches return no results.
- **Azure errors** (wrong key, unreachable endpoint, throttling, rejected index changes): logged with a hint about what to check. Searches return no results instead of throwing.
- **Throttling** while indexing is retried with exponential backoff.

## Troubleshooting

| Symptom | What to check |
|---|---|
| `The Azure AI Search provider is disabled because of configuration errors` | The log line lists each setting to fix. |
| `rejected the API key` | Use an **admin** key. Query keys can only search. |
| `could not be reached` | Check the `Endpoint` URL and that the site can reach `*.search.windows.net`. |
| Index is empty | Content that existed before the provider was added needs a [rebuild](#rebuilding-indexes). Indexing also starts about a minute after boot. |
| The Azure portal shows 0 documents | The portal's document count and storage figures lag behind. Use *Search explorer* to see what is in the index. |
| A filter, facet or sort does nothing | The field is not declared. Look for a warning in the log, and add it under `Fields`. |
| `rejected the index definition` | An existing field changed type. Rebuild the index. |
| `lists the content type …, which does not exist` | A `ContentTypes` alias is wrong, or the content type has not been created yet. Fields are refreshed when it is created. |

## Test site

The repository contains a test site with a demo content generator and a small JSON API.

```bash
cd src/Umbraco.Community.Search.Provider.AzureAI.TestSite
dotnet user-secrets set "AzureSearchProvider:Endpoint" "https://<service>.search.windows.net"
dotnet user-secrets set "AzureSearchProvider:ApiKey" "<admin key>"
dotnet run
```

The first run installs Umbraco unattended. The backoffice login is in `appsettings.Development.json`.
The test site's two indexes, `TestSite_Content` and `TestSite_Products`, are configured in `appsettings.json`.

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/seed/generate?count=200` | Generate demo articles and products |
| GET | `/api/search?q=steel&category=Food&sort=price-asc` | Search `TestSite_Content` (sort: `price-asc`, `price-desc`, `newest`) |
| GET | `/api/search?index=TestSite_Products` | Search the products-only index |
| GET | `/api/search/status?index=TestSite_Content` | Document count and health |
| POST | `/api/search/rebuild?index=TestSite_Content` | Rebuild an index |

## Contributing

Contributions are welcome. Please read the [contributing guidelines](CONTRIBUTING.md).

Each Umbraco major has its own branch (`v17`, `v18`, `v19`). Fix things on the lowest branch they apply to, then bring them forward.

```bash
dotnet test src/Umbraco.Community.Search.Provider.AzureAI.Tests
```

## Acknowledgements

Built on [Umbraco Search](https://github.com/umbraco/Umbraco.Cms.Search), and inspired by
[Kjac.SearchProvider.Algolia](https://github.com/kjac/Kjac.SearchProvider.Algolia).
The repository structure comes from Lotte Pitcher's [opinionated package starter](https://github.com/LottePitcher/opinionated-package-starter).

## License

[MIT](../LICENSE)
