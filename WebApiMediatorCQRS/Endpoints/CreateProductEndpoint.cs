using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public sealed class CreateProductEndpoint
{
    [Post("/products")]
    [Produces(StatusCodes.Status201Created)]
    [Produces(StatusCodes.Status400BadRequest)]
    public static async Task<IResult> Handle(
        CreateProductRequest request,
        IMediator mediator,
        IValidator<CreateProductCommand> validator,
        CancellationToken cancellationToken
    )
    {
        var command = new CreateProductCommand(
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
            ProductMutationStatus.Success => TypedResults.Created(
                $"/products/{result.Product!.ProductId}",
                result.Product
            ),
            ProductMutationStatus.InvalidSupplier => ProductEndpointResults.InvalidReference(
                nameof(request.SupplierId),
                request.SupplierId
            ),
            ProductMutationStatus.InvalidCategory => ProductEndpointResults.InvalidReference(
                nameof(request.CategoryId),
                request.CategoryId
            ),
            _ => throw new InvalidOperationException("Unexpected product creation result."),
        };
    }
}

internal static class ProductEndpointResults
{
    public static IResult InvalidReference(string propertyName, int? value) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]>
            {
                [propertyName] = [$"Referenced resource with ID '{value}' does not exist."],
            }
        );

    public static IResult Conflict(int productId) =>
        TypedResults.Conflict(
            new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The product cannot be deleted.",
                Detail = $"Product '{productId}' is referenced by existing order details.",
            }
        );
}