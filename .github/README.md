# Umbraco.Community.Search.Provider.AzureAI

[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.Search.Provider.AzureAI?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.Search.Provider.AzureAI)
[![GitHub license](https://img.shields.io/github/license/busrasengul/Umbraco.Community.Search.Provider.AzureAI?color=8AB803)](../LICENSE)

<img src="../docs/icon.png" alt="Logo" width="128" align="right" />

An [Azure AI Search](https://learn.microsoft.com/azure/search/) provider for [Umbraco Search](https://github.com/umbraco/Umbraco.Cms.Search).

Umbraco keeps your Azure indexes up to date as content is published, moved or deleted. You query them through the
Umbraco Search `ISearcher` abstraction, with full text search, filters, facets, sorting, cultures and protected content.

## Versions

| Package | Umbraco | Umbraco Search | .NET | Branch |
|---|---|---|---|---|
| 17.x | 17 | 17.2+ | 10 | `v17` |
| 18.x | 18 | 18.x | 10 | `v18` |
| 19.0.0-beta1 | 19 (pre-release) | 19.x | 11 | `v19` |

## Installation

```bash
dotnet add package Umbraco.Community.Search.Provider.AzureAI
```

Add the provider in a composer, after Umbraco Search:

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

## Configuration

Everything is configured in `appsettings.json`, under `AzureSearchProvider`.
Keep the API key out of source control with [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or environment variables.

```json
{
  "AzureSearchProvider": {
    "Endpoint": "https://my-service.search.windows.net",
    "ApiKey": "<admin api key>",
    "IndexPrefix": "",
    "RegisterDefaultIndexes": false,
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

| Setting | Default | Description |
|---|---|---|
| `Endpoint` | | The search service URL. Must be `https`. |
| `ApiKey` | | An **admin** key. Query keys cannot create indexes or upload documents. |
| `IndexPrefix` | `""` | Prefixed to every index name, so several sites or environments can share one service. |
| `RegisterDefaultIndexes` | `false` | Moves the four default Umbraco Search indexes (published content, draft content, media, members) to Azure. Each is a separate Azure index, so check your tier's index limit. |
| `Indexes` | `[]` | Extra indexes to create and keep up to date. See below. |
| `Fields` | `[]` | Fields declared for every Azure index. |
| `FuzzySearch` | `true` | Allows one typo in query terms of four characters or more. |
| `MaxFacetValues` | `100` | The most values returned per keyword facet. |
| `BatchSize` | `250` | Documents uploaded per request. |
| `FlushDelayMilliseconds` | `1000` | How long changes are collected before a partial batch is uploaded. |
| `MaxIndexingAttempts` | `8` | Upload attempts when Azure throttles (HTTP 429/503), with exponential backoff. |

The Azure index name is the prefix plus the alias, in lowercase, with underscores turned into dashes: `Umb_PublishedContent` becomes `umb-publishedcontent`.

### Indexes

| Setting | Default | Description |
|---|---|---|
| `Alias` | | The Umbraco Search index alias you query with. |
| `ObjectTypes` | `["Document"]` | Any of `Document`, `Media` and `Member`. |
| `ContentState` | `Published` | `Published` or `Draft`. Media and member indexes must use `Draft`. |
| `ContentTypes` | `[]` | Content type aliases to include. Empty includes everything. |
| `AutoFields` | `false` | Declares a filterable field for every property of `ContentTypes` (see below). |
| `Fields` | `[]` | Fields declared for this index only. These override automatic fields. |

### Fields

Every property is full text searchable without any setup. To **filter, facet or sort** on a property, declare it as a field:

```json
"Fields": [
  { "PropertyName": "category", "FieldValues": "Keywords", "Facetable": true },
  { "PropertyName": "price", "FieldValues": "Decimals", "Facetable": true, "Sortable": true }
]
```

`FieldValues` must match how Umbraco Search indexes the property: `Keywords`, `Integers`, `Decimals`, `DateTimeOffsets` or `Texts`.

A sortable number or date holds a single value per document. Leave `Sortable` off for properties that hold several values.

### Automatic fields

With `AutoFields`, the provider reads the index's content types on startup and declares fields from their property editors.
It also does this again whenever one of them changes:

| Property editor | Field | Facetable | Sortable |
|---|---|---|---|
| Textstring, Textarea, Repeatable textstrings | `Texts` | | |
| Numeric, True/false | `Integers` | ✓ | ✓ |
| Decimal | `Decimals` | ✓ | ✓ |
| Slider | `Decimals` | ✓ | |
| Date and time pickers | `DateTimeOffsets` | ✓ | ✓ |
| Tags, Dropdown, Radio button list, Checkbox list, Content picker, Multinode tree picker | `Keywords` | ✓ | |

Rich text, block editors, media pickers and labels are searchable but not declared, because they do not have one value type.
When the same alias is a different type on two content types, it is skipped with a warning. Declare it under `Fields` to choose.

Adding fields to an existing index happens in place. **Changing the type of a field requires a rebuild**, which recreates the Azure index.

## Searching

Resolve the searcher for your index through Umbraco Search:

```csharp
ISearcher searcher = searcherResolver.GetRequiredSearcher("Products");

SearchResult result = await searcher.SearchAsync(
    "Products",
    query: "running shoes",
    filters: [new KeywordFilter("category", ["Sport"], false)],
    facets: [new KeywordFacet("brand"), new DecimalRangeFacet("price", [new("budget", null, 50m), new("premium", 50m, null)])],
    sorters: [new DecimalSorter("price", Direction.Ascending)],
    culture: "en-US",
    take: 20);
```

Filters, facets and sorters on undeclared fields are ignored with a warning in the log.

## Error handling

The provider never stops the site from starting:

- **Missing or invalid configuration**: every problem is logged once on startup with the setting that caused it. No Azure indexes are registered, indexing is skipped and Azure searches return no results.
- **Azure errors** (wrong key, unreachable endpoint, throttling, rejected index changes): logged with a hint about what to check. Searches return no results rather than throwing.

## Test site

The repository contains a test site with demo content:

```bash
cd src/Umbraco.Community.Search.Provider.AzureAI.TestSite
dotnet user-secrets set "AzureSearchProvider:Endpoint" "https://<service>.search.windows.net"
dotnet user-secrets set "AzureSearchProvider:ApiKey" "<admin api key>"
dotnet run
```

Log in at `/umbraco` with the credentials in `appsettings.Development.json`. Then, in development:

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/seed/generate?count=500` | Generate demo articles and products |
| GET | `/api/search?q=cloud&category=Technology&sort=price-asc` | Search `TestSite_Content` |
| GET | `/api/search?index=TestSite_Products` | Search the products-only index |
| GET | `/api/search/status?index=TestSite_Content` | Document count and health |
| POST | `/api/search/rebuild?index=TestSite_Content` | Rebuild an index |

## Contributing

Contributions are welcome. Please read the [contributing guidelines](CONTRIBUTING.md).

Run the unit tests with:

```bash
dotnet test src/Umbraco.Community.Search.Provider.AzureAI.Tests
```

## Acknowledgements

Built on [Umbraco Search](https://github.com/umbraco/Umbraco.Cms.Search). The repository structure comes from Lotte Pitcher's
[opinionated package starter](https://github.com/LottePitcher/opinionated-package-starter).
