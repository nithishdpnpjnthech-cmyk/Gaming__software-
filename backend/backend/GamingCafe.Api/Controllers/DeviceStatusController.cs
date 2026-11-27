using GamingCafe.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamingCafe.Api.Controllers;

[ApiController]
[Authorize(Policy = "devices.view")]
[Route("api/devices")]
public sealed class DeviceStatusController(
    DeviceStatusService service) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetAll(
        CancellationToken ct)
    {
        return Ok(await service.GetAllAsync(ct));
    }

    [HttpGet("{deviceId:guid}/status")]
    public async Task<IActionResult> GetById(
        Guid deviceId,
        CancellationToken ct)
    {
        var result = await service.GetByIdAsync(
            deviceId, ct);

        if (result is null)
        {
            return NotFound(new
            {
                message = "Device not found."
            });
        }

        return Ok(result);
    }

    [HttpGet("status/summary")]
    public async Task<IActionResult> GetSummary(
        CancellationToken ct)
    {
        return Ok(await service.GetSummaryAsync(ct));
    }
}