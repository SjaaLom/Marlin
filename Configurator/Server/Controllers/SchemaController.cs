using Configurator.Server.Services;
using Configurator.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Configurator.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchemaController : ControllerBase
{
    private readonly ISchemaService _schemaService;

    public SchemaController(ISchemaService schemaService)
    {
        _schemaService = schemaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ConfigGroup>>> GetSchema()
    {
        try
        {
            var schema = await _schemaService.GetSchemaAsync();
            return Ok(schema);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("check-python")]
    public async Task<ActionResult<bool>> CheckPython()
    {
        return await _schemaService.IsPythonInstalledAsync();
    }
}
