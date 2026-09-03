using AutoMapper;
using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public class AddCustomerEndpoint
{
    [Post("/customers")]
    [Produces(StatusCodes.Status201Created)]
    [Produces(StatusCodes.Status400BadRequest)]
    [Produces(StatusCodes.Status409Conflict)]
    public static async Task<IResult> Handle(
        AddCustomerRequest request,
        IMediator mediator,
        IMapper mapper,
        IValidator<AddCustomerCommand> validator,
        CancellationToken cancellationToken
    )
    {
        var command = mapper.Map<AddCustomerCommand>(request);
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var response = await mediator.Send(command, cancellationToken);
        if (response == null)
            return Results.Conflict();

        return Results.Created($"/customers/{response.CustomerId}", mapper.Map<AddCustomerResponse>(response));
    }
}
