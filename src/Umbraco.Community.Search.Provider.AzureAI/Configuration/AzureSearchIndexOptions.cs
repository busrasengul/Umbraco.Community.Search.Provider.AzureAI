using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.Search.Provider.AzureAI.Configuration;

public enum AzureIndexContentState
{
    /// <summary>Only published content is indexed.</summary>
    Published,

    /// <summary>The latest saved version is indexed. Required for media and members.</summary>
    Draft,
}

/// <summary>
/// An index declared in configuration under <c>AzureSearchProvider:Indexes</c>.
/// </summary>
public sealed class AzureSearchIndexOptions
{
    public string Alias { get; set; } = string.Empty;

    public UmbracoObjectTypes[] ObjectTypes { get; set; } = [UmbracoObjectTypes.Document];

    public AzureIndexContentState ContentState { get; set; } = AzureIndexContentState.Published;

    /// <summary>Content type aliases to include. Empty means every content type.</summary>
    public string[] ContentTypes { get; set; } = [];

    /// <summary>
    /// Declares filterable fields for every property of <see cref="ContentTypes"/>, based on its property editor.
    /// </summary>
    public bool AutoFields { get; set; }

    /// <summary>Fields declared explicitly. These win over automatically declared fields.</summary>
    public AzureSearchFieldOptions.Field[] Fields { get; set; } = [];
}
