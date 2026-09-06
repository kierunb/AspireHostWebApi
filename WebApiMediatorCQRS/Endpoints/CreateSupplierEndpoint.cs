using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public sealed class CreateSupplierEndpoint
{
    [Post("/suppliers")]
    [Produces(StatusCodes.Status201Created)]
    [Produces(StatusCodes.Status400BadRequest)]
    public static async Task<IResult> Handle(
        CreateSupplierRequest request,
        IMediator mediator,
        IValidator<CreateSupplierCommand> validator,
        CancellationToken cancellationToken
    )
    {
        var command = new CreateSupplierCommand(
            request.CompanyName,
            request.ContactName,
            request.ContactTitle,
            request.Address,
            request.City,
            request.Region,
            request.PostalCode,
            request.Country,
            request.Phone,
            request.Fax,
            request.HomePage
        );
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var result = await mediator.Send(command, cancellationToken);
        return result.Status switch
        {
            SupplierMutationStatus.Success => TypedResults.Created(
                $"/suppliers/{result.Supplier!.SupplierId}",
                result.Supplier
            ),
            _ => throw new InvalidOperationException("Unexpected supplier creation result."),
        };
    }
}

internal static class SupplierEndpointResults
{
    public static IResult Conflict(int supplierId) =>
        TypedResults.Conflict(
            new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The supplier cannot be deleted.",
                Detail = $"Supplier '{supplierId}' is referenced by existing products.",
            }
        );
}