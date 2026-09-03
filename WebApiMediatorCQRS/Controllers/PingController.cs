using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Controllers;

[ApiController]
[Route("mvc")]
public class PingController(IMediator mediator) : ControllerBase
{
    [HttpPost("simple")]
    public async Task<IActionResult> Ping(PingCommand command)
    {
        // mediator
        var response = await mediator.Send(command);

        return Ok(response);
    }

    [HttpPost("mapper")]
    public async Task<IActionResult> PingMapper(PingRequest request, [FromServices] IMapper mapper)
    {
        // mediator & automapper (to map request models to message/event/domain models)
        var response = await mediator.Send(mapper.Map<PingCommand>(request));

        return Ok(mapper.Map<PingResponse>(response));
    }

    [HttpPost("validation")]
    public async Task<IActionResult> PingValidation(
        PingCommand command,
        [FromServices] IValidator<PingCommand> validator
    )
    {
        // validation, can be abstracted away using filters or via a MediatR behavior
        var validationResult = await validator.ValidateAsync(command);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.ToDictionary());

        // mediator
        var response = await mediator.Send(command);

        return Ok(response);
    }
}
