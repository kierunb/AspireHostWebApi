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

        for (var i = 0; i < 100; i++)
        {
            using var allowedResponse = await httpClient.GetAsync("/swagger/v1/swagger.json", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        }

        // Act
        using var rejectedResponse = await httpClient.GetAsync("/swagger/v1/swagger.json", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        });
}
