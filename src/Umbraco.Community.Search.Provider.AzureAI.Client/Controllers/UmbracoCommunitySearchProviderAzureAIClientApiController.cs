using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Umbraco.Community.Search.Provider.AzureAI.Client.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Umbraco.Community.Search.Provider.AzureAI.Client")]
    public class UmbracoCommunitySearchProviderAzureAIClientApiController : UmbracoCommunitySearchProviderAzureAIClientApiControllerBase
    {

        [HttpGet("ping")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        public string Ping() => "Pong";
    }
}
