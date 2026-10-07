using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.Search.Provider.AzureAI.TestSite.Seeding;

namespace Umbraco.Community.Search.Provider.AzureAI.TestSite.Controllers;

/// <summary>
/// Generates demo articles and products. Development only.
/// </summary>
[ApiController]
[Route("api/seed")]
public sealed class SeedApiController : ControllerBase
{
    private readonly DemoContentManager _manager;
    private readonly IWebHostEnvironment _environment;

    public SeedApiController(DemoContentManager manager, IWebHostEnvironment environment)
    {
        _manager = manager;
        _environment = environment;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromQuery] int count = 500)
    {
        if (_environment.IsDevelopment() is false)
        {
            return NotFound();
        }

        DateTime started = DateTime.UtcNow;
        var created = await _manager.GenerateAsync(count);
        return Ok(new { created, seconds = (DateTime.UtcNow - started).TotalSeconds });
    }

    [HttpPost("clear")]
    public IActionResult Clear()
        => _environment.IsDevelopment() ? Ok(new { deleted = _manager.Clear() }) : NotFound();

    [HttpGet("status")]
    public IActionResult Status()
        => Ok(new { counts = _manager.PublishedCounts() });
}
