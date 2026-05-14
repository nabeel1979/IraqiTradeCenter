using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    private IMediator? _mediator;
    protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    protected IActionResult HandleResult<T>(Result<T> r)
        => r.IsSuccess ? Ok(new { success = true, data = r.Value })
                       : BadRequest(new { success = false, errors = r.Errors });

    protected IActionResult HandleResult(Result r)
        => r.IsSuccess ? Ok(new { success = true })
                       : BadRequest(new { success = false, errors = r.Errors });
}
