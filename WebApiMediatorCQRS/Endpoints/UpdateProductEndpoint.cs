using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public sealed class UpdateProductEndpoint
{
    [Put("/products/{id:int}")]
    [Produces(StatusCodes.Status200OK)]
    [Produces(StatusCodes.Status400BadRequest)]
    [Produces(StatusCodes.Status404NotFound)]
    public static async Task<IResult> Handle(
        int id,
        UpdateProductRequest request,
        IMediator mediator,
        IValidator<UpdateProductCommand> validator,
        CancellationToken cancellationToken
    )
    {
        var command = new UpdateProductCommand(
            id,
            request.ProductName,
            request.SupplierId,
            request.CategoryId,
            request.QuantityPerUnit,
            request.UnitPrice,
            request.UnitsInStock,
            request.UnitsOnOrder,
            request.ReorderLevel,
            request.Discontinued
        );
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var result = await mediator.Send(command, cancellationToken);
        return result.Status switch
        {
            ProductMutationStatus.Success => TypedResults.Ok(result.Product),
            ProductMutationStatus.NotFound => TypedResults.NotFound(),
            ProductMutationStatus.InvalidSupplier => ProductEndpointResults.InvalidReference(
                nameof(request.SupplierId),
                request.SupplierId
            ),
            ProductMutationStatus.InvalidCategory => ProductEndpointResults.InvalidReference(
                nameof(request.CategoryId),
                request.CategoryId
            ),
            _ => throw new InvalidOperationException("Unexpected product update result."),
        };
    }
}