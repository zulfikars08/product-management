using System.Net;
using ProductManagement.Tests.Auth;

namespace ProductManagement.Tests.Frontend;

public sealed class FrontendIntegrationTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MainPage_IsPublicAndContainsPolishedAuthenticationUi()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Keep product records clear and current", html);
        Assert.Contains("id=\"loginForm\"", html);
        Assert.Contains("id=\"productPanel\"", html);
        Assert.Contains("data-password-toggle=\"loginPassword\"", html);
        Assert.Contains("data-password-toggle=\"registerPassword\"", html);
        Assert.Contains("id=\"toastRegion\"", html);
        Assert.Contains("aria-live=\"polite\"", html);
        Assert.Contains("id=\"deleteModal\"", html);
        Assert.Contains("/js/product-management.js", html);
    }

    [Fact]
    public async Task ProductManagementScript_IsReachableAndContainsCentralizedUiBehavior()
    {
        var response = await _client.GetAsync("/js/product-management.js");
        var script = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("function initializePasswordToggles()", script);
        Assert.Contains("function showToast(", script);
        Assert.Contains("function apiFetch(", script);
        Assert.Contains("/api/products", script);
        Assert.Contains("Authorization", script);
        Assert.Contains("sessionStorage", script);
        Assert.DoesNotContain("window.confirm", script);
        Assert.DoesNotContain("window.alert", script);
    }
}
