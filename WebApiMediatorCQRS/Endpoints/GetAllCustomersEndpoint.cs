using MediatR;
using Microsoft.AspNetCore.OutputCaching;
using Reprise;
using WebApiMediatorCQRS.Queries;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public class GetAllCustomersEndpoint
{
    [Get("/customers")]
    [Produces(StatusCodes.Status200OK)]
    [OutputCache]
    public static async Task<IResult> Handle(IMediator mediator)
    {
        var response = await mediator.Send(new GetAllCustomersQuery());

        return TypedResults.Ok(response);
    }
}
