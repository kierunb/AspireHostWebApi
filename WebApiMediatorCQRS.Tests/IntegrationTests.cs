using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApiMediatorCQRS.Tests.Tests;

public class IntegrationTests
{
    [Fact]
    public async Task GetSwaggerReturnsOkStatusCode()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();

        // Act
        using var response = await httpClient.GetAsync("/swagger/v1/swagger.json", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestsBeyondGlobalRateLimitReturnTooManyRequests()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();

        var tasks = new List<Task<HttpResponseMessage>>(capacity: 110);
        for (var i = 0; i < 110; i++)
        {
            tasks.Add(httpClient.GetAsync("/swagger/v1/swagger.json", cancellationToken));
        }

        var responses = await Task.WhenAll(tasks);

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.TooManyRequests);

        foreach (var response in responses)
        {
            response.Dispose();
        }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
}
