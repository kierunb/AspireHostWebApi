using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Commands;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public sealed class UpdateSupplierEndpoint
{
    [Put("/suppliers/{id:int}")]
    [Produces(StatusCodes.Status200OK)]
    [Produces(StatusCodes.Status400BadRequest)]
    [Produces(StatusCodes.Status404NotFound)]
    public static async Task<IResult> Handle(
        int id,
        UpdateSupplierRequest request,
        IMediator mediator,
        IValidator<UpdateSupplierCommand> validator,
        CancellationToken cancellationToken
    )
    {
        var command = new UpdateSupplierCommand(
            id,
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
            SupplierMutationStatus.Success => TypedResults.Ok(result.Supplier),
            SupplierMutationStatus.NotFound => TypedResults.NotFound(),
            _ => throw new InvalidOperationException("Unexpected supplier update result."),
        };
    }
}