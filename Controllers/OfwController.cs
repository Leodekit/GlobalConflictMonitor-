using GlobalConflictMonitor.Application.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OfwController : ControllerBase
{
    private readonly OfwRiskService _service;

    public OfwController(OfwRiskService service)
    {
        _service = service;
    }

    [HttpGet("risk-index")]
    public async Task<IActionResult> GetRiskIndex()
    {
        return Ok(await _service.GetRiskIndex());
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        return Ok(await _service.GetOfwAlerts());
    }
}