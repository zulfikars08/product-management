using System.Net;
using ProductManagement.Tests.Auth;

namespace ProductManagement.Tests.Frontend;

public sealed class FrontendIntegrationTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MainPage_IsPublicAndContainsProductManagementUi()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Manage your products", html);
        Assert.Contains("id=\"loginForm\"", html);
        Assert.Contains("id=\"productPanel\"", html);
        Assert.Contains("/js/product-management.js", html);
    }

    [Fact]
    public async Task ProductManagementScript_IsReachableAndUsesProductApi()
    {
        var response = await _client.GetAsync("/js/product-management.js");
        var script = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/products", script);
        Assert.Contains("Authorization", script);
        Assert.Contains("sessionStorage", script);
    }
}
