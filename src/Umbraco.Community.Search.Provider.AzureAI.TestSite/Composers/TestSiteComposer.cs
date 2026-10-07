using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Search.BackOffice.DependencyInjection;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;
using Umbraco.Community.Search.Provider.AzureAI.DependencyInjection;
using Umbraco.Community.Search.Provider.AzureAI.TestSite.Seeding;

namespace Umbraco.Community.Search.Provider.AzureAI.TestSite.Composers;

public sealed class TestSiteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Examine keeps serving the default indexes unless AzureSearchProvider:RegisterDefaultIndexes is true.
        // The Azure provider is added last so it wins when it takes over the defaults.
        builder
            .AddSearchCore()
            .AddExamineSearchProvider()
            .AddBackOfficeSearch()
            .AddAzureSearchProvider();

        builder.Services.AddTransient<DemoContentManager>();
    }
}
