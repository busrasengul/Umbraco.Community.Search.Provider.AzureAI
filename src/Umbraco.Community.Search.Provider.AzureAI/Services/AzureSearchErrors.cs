using Azure;
using Umbraco.Community.Search.Provider.AzureAI.Configuration;

namespace Umbraco.Community.Search.Provider.AzureAI.Services;

/// <summary>
/// Turns Azure AI Search failures into messages that point at the setting to fix.
/// </summary>
internal static class AzureSearchErrors
{
    private const string Section = AzureSearchOptions.SectionName;

    public static string Describe(RequestFailedException exception)
        => exception.Status switch
        {
            0 => $"Azure AI Search could not be reached. Check {Section}:Endpoint and that the site can reach the service.",
            401 or 403 => $"Azure AI Search rejected the API key. {Section}:ApiKey must be an admin key, not a query key.",
            404 => $"Azure AI Search returned 404. Check {Section}:Endpoint points at the right search service.",
            400 when exception.Message.Contains("field", StringComparison.OrdinalIgnoreCase)
                => $"Azure AI Search rejected the index definition, usually because an existing field changed type. Rebuild the index to recreate it. Details: {exception.Message}",
            429 or 503 => $"Azure AI Search is throttling requests ({exception.Status}). Consider a higher tier or a smaller {Section}:BatchSize.",
            _ => $"Azure AI Search returned {exception.Status}: {exception.Message}",
        };
}
