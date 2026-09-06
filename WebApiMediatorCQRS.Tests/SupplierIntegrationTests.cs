using System.Text.Json;

namespace WebApiMediatorCQRS.Tests.Tests;

public class SupplierIntegrationTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GivenDevelopmentAppHost_WhenSupplierOpenApiFetched_ExpectedTwoPathsAndFiveOperations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireAppHost_AppHost>(
            cancellationToken
        );
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost
            .BuildAsync(cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        Assert.Equal("Development", appHost.Environment.EnvironmentName);

        using var httpClient = app.CreateHttpClient("webapimediatorcqrs");
        await app
            .ResourceNotifications.WaitForResourceHealthyAsync(
                "webapimediatorcqrs",
                cancellationToken
            )
            .WaitAsync(DefaultTimeout, cancellationToken);
        using var response = await httpClient.GetAsync("/swagger/v1/swagger.json", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var openApiStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var openApiDocument = await JsonDocument.ParseAsync(
            openApiStream,
            cancellationToken: cancellationToken
        );

        var paths = openApiDocument.RootElement.GetProperty("paths");
        var supplierPathNames = paths
            .EnumerateObject()
            .Where(path => path.Name.StartsWith("/suppliers", StringComparison.Ordinal))
            .Select(path => path.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["/suppliers", "/suppliers/{id}"], supplierPathNames);

        var collectionPath = paths.GetProperty("/suppliers");
        var itemPath = paths.GetProperty("/suppliers/{id}");

        AssertOperationStatusCodes(collectionPath, "get", "200");
        AssertOperationStatusCodes(collectionPath, "post", "201", "400");
        AssertOperationStatusCodes(itemPath, "get", "200", "400", "404");
        AssertOperationStatusCodes(itemPath, "put", "200", "400", "404");
        AssertOperationStatusCodes(itemPath, "delete", "204", "400", "404", "409");
    }

    private static void AssertOperationStatusCodes(
        JsonElement path,
        string operationName,
        params string[] expectedStatusCodes
    )
    {
        var actualStatusCodes = path
            .GetProperty(operationName)
            .GetProperty("responses")
            .EnumerateObject()
            .Select(response => response.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedStatusCodes.Order(StringComparer.Ordinal), actualStatusCodes);
    }
}