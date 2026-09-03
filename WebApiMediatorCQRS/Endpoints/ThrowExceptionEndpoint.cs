using Reprise;

namespace WebApiMediatorCQRS.Endpoints;

[Endpoint]
public class ThrowExceptionEndpoint
{
    [Get("/throw-exception")]
    [Produces(StatusCodes.Status500InternalServerError)]
    public static async Task<IResult> Handle()
    {
        throw new Exception("This is an exception");
    }
}
