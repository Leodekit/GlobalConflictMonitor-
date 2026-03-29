using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SituationController : ControllerBase
{
    private readonly SituationReportService _service;

    public SituationController(SituationReportService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetReport()
    {
        return Ok(await _service.GenerateReport());
    }
}