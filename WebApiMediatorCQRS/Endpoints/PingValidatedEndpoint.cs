using AutoMapper;
using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

public class PingRequestValidator : AbstractValidator<PingRequest>
{
    public PingRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().Length(3, 50);
    }
}

[Endpoint]
public class PingValidatedEndpoint
{
    [Post("repr/ping-validated")]
    [Produces(StatusCodes.Status201Created)]
    [Produces(StatusCodes.Status400BadRequest)]
    public static async Task<IResult> Handle(
        PingRequest request,
        IMediator mediator,
        IMapper mapper,
        IValidator<PingRequest> validator
    )
    {
        //validator.ValidateAndThrow(request);

        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await mediator.Send(mapper.Map<PingCommand>(request)));
    }
}
