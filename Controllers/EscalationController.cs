using GlobalConflictMonitor.Application.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class EscalationController : ControllerBase
{
    private readonly ConflictEscalationService _service;

    public EscalationController(ConflictEscalationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetEscalations()
    {
        var data = await _service.DetectEscalations();
        return Ok(data);
    }
}