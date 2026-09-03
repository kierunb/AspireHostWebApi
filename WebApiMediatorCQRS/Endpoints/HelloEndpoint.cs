using Reprise;

namespace WebApiMediatorCQRS.Endpoints;

    
public class HelloEndpoint
{
    [Get("repr/hello")]
    public static string Handle() => "Hello, world!";
}
