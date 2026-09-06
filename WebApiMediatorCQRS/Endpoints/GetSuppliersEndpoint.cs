using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.Queries;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public sealed class GetSuppliersEndpoint
{
    [Get("/suppliers")]
    [Produces(StatusCodes.Status200OK)]
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var suppliers = await mediator.Send(new GetAllSuppliersQuery(), cancellationToken);
        return TypedResults.Ok(suppliers);
    }
}

[Endpoint]
public sealed class GetSupplierByIdEndpoint
{
    [Get("/suppliers/{id:int}")]
    [Produces(StatusCodes.Status200OK)]
    [Produces(StatusCodes.Status400BadRequest)]
    [Produces(StatusCodes.Status404NotFound)]
    public static async Task<IResult> Handle(
        int id,
        IMediator mediator,
        IValidator<GetSupplierByIdQuery> validator,
        CancellationToken cancellationToken
    )
    {
        var query = new GetSupplierByIdQuery(id);
        var validationResult = await validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var supplier = await mediator.Send(query, cancellationToken);
        return supplier is null ? TypedResults.NotFound() : TypedResults.Ok(supplier);
    }
}