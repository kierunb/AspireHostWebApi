using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public sealed class DeleteSupplierEndpoint
{
    [Delete("/suppliers/{id:int}")]
    [Produces(StatusCodes.Status204NoContent)]
    [Produces(StatusCodes.Status400BadRequest)]
    [Produces(StatusCodes.Status404NotFound)]
    [Produces(StatusCodes.Status409Conflict)]
    public static async Task<IResult> Handle(
        int id,
        IMediator mediator,
        IValidator<DeleteSupplierCommand> validator,
        CancellationToken cancellationToken
    )
    {
        var command = new DeleteSupplierCommand(id);
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var status = await mediator.Send(command, cancellationToken);
        return status switch
        {
            SupplierMutationStatus.Success => TypedResults.NoContent(),
            SupplierMutationStatus.NotFound => TypedResults.NotFound(),
            SupplierMutationStatus.Conflict => SupplierEndpointResults.Conflict(id),
            _ => throw new InvalidOperationException("Unexpected supplier deletion result."),
        };
    }
}