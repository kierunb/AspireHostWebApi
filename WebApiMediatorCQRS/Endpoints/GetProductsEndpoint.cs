using FluentValidation;
using MediatR;
using Reprise;
using WebApiMediatorCQRS.Queries;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public sealed class GetProductsEndpoint
{
    [Get("/products")]
    [Produces(StatusCodes.Status200OK)]
    public static async Task<IResult> Handle(
        IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var products = await mediator.Send(new GetAllProductsQuery(), cancellationToken);
        return TypedResults.Ok(products);
    }
}

[Endpoint]
public sealed class GetProductByIdEndpoint
{
    [Get("/products/{id:int}")]
    [Produces(StatusCodes.Status200OK)]
    [Produces(StatusCodes.Status400BadRequest)]
    [Produces(StatusCodes.Status404NotFound)]
    public static async Task<IResult> Handle(
        int id,
        IMediator mediator,
        IValidator<GetProductByIdQuery> validator,
        CancellationToken cancellationToken
    )
    {
        var query = new GetProductByIdQuery(id);
        var validationResult = await validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var product = await mediator.Send(query, cancellationToken);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }
}