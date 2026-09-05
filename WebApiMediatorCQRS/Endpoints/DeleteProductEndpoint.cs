using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public sealed class DeleteProductEndpoint
{
    [Delete("/products/{id:int}")]
    [Produces(StatusCodes.Status204NoContent)]
    [Produces(StatusCodes.Status400BadRequest)]
    [Produces(StatusCodes.Status404NotFound)]
    [Produces(StatusCodes.Status409Conflict)]
    public static async Task<IResult> Handle(
        int id,
        IMediator mediator,
        IValidator<DeleteProductCommand> validator,
        CancellationToken cancellationToken
    )
    {
        var command = new DeleteProductCommand(id);
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var status = await mediator.Send(command, cancellationToken);
        return status switch
        {
            ProductMutationStatus.Success => TypedResults.NoContent(),
            ProductMutationStatus.NotFound => TypedResults.NotFound(),
            ProductMutationStatus.Conflict => ProductEndpointResults.Conflict(id),
            _ => throw new InvalidOperationException("Unexpected product deletion result."),
        };
    }
}