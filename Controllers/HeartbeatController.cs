using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Services;

namespace SProtectAgentWeb.Api.Controllers;

/// <summary>守护进程心跳接口。</summary>
[ApiController]
[Route("api/[controller]")]
public class HeartbeatController : ControllerBase
{
    private readonly HeartbeatRegistry _registry;

    public HeartbeatController(HeartbeatRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>守护进程上报心跳。</summary>
    [AllowAnonymous]
    [HttpPost("ping")]
    public ActionResult<HeartbeatStatus> Ping([FromBody] HeartbeatRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new HeartbeatStatus
            {
                Accepted = false,
                Message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)),
                ReceivedAt = DateTimeOffset.UtcNow
            });
        }

        var status = _registry.RecordHeartbeat(request);
        return status.Accepted ? Ok(status) : Unauthorized(status);
    }

    /// <summary>获取当前在线守护进程列表。</summary>
    [Authorize]
    [HttpGet("status")]
    public ActionResult<IReadOnlyCollection<HeartbeatSnapshot>> Status()
    {
        return Ok(_registry.GetSnapshots());
    }
}
