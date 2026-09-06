using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.Queries;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public class GetCustomerByIdEndpoint
{
    [Get("/customers/{id}")]
    [Produces(StatusCodes.Status200OK)]
    [Produces(StatusCodes.Status404NotFound)]
    [Produces(StatusCodes.Status400BadRequest)]
    public static async Task<IResult> Handle(
        string id,
        IMediator mediator,
        IValidator<GetCustomerByIdQuery> validator,
        CancellationToken cancellationToken
    )
    {
        // manual validation or enable ValidationBehavior
        var validationResult = await validator.ValidateAsync(new GetCustomerByIdQuery(id), cancellationToken);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var response = await mediator.Send(new GetCustomerByIdQuery(id), cancellationToken);
        if (response == null)
            return Results.NotFound();

        return TypedResults.Ok(response);
    }
}
