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
public static async Task<IResult> Handle(IMediator mediator, CancellationToken cancellationToken)
{
    var response = await mediator.Send(new GetAllCustomersQuery(), cancellationToken);

    return TypedResults.Ok(response);
}
    }
}
