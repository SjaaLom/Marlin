using Configurator.Server.Services;
using Configurator.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Configurator.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IConfigurationService _configService;

    public ConfigController(IConfigurationService configService)
    {
        _configService = configService;
    }

    [HttpPost]
    public async Task<IActionResult> SaveConfig([FromBody] List<MarlinSetting> settings)
    {
        try
        {
            await _configService.SaveConfigurationAsync(settings);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
