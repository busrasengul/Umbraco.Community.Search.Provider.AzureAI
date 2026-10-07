# Umbraco.Community.Search.Provider.AzureAI

An [Azure AI Search](https://learn.microsoft.com/azure/search/) provider for [Umbraco Search](https://github.com/umbraco/Umbraco.Cms.Search).

Umbraco keeps your Azure indexes up to date as content changes. You query them through Umbraco Search, with full text search,
filters, facets, sorting, cultures and protected content.

## Getting started

Add the provider in a composer:

```csharp
public class SearchComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder
            .AddSearchCore()
            .AddAzureSearchProvider();
}
```

Then configure it in `appsettings.json`:

```json
{
  "AzureSearchProvider": {
    "Endpoint": "https://my-service.search.windows.net",
    "ApiKey": "<admin api key>",
    "Indexes": [
      { "Alias": "Products", "ContentTypes": [ "product" ], "AutoFields": true }
    ]
  }
}
```

`AutoFields` declares filterable, facetable and sortable fields from the content types' property editors.
Set `RegisterDefaultIndexes` to `true` to move the default Umbraco Search indexes to Azure as well.

A missing or invalid configuration is logged on startup and never stops the site.

Full documentation: https://github.com/busrasengul/Umbraco.Community.Search.Provider.AzureAI
