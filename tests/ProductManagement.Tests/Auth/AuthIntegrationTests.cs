using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ProductManagement.Application.Auth.Models;

namespace ProductManagement.Tests.Auth;

public sealed class AuthIntegrationTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreatedAndToken()
    {
        var response = await RegisterAsync(UniqueEmail());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.NotEmpty(auth.Token);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var response = await RegisterAsync("not-an-email");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        var response = await RegisterAsync(UniqueEmail(), "short");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateNormalizedEmail_ReturnsConflict()
    {
        var email = UniqueEmail();
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(email.ToUpperInvariant())).StatusCode);

        var duplicate = await RegisterAsync(email);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsToken()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await response.Content.ReadFromJsonAsync<AuthResponse>())?.Token));
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ReturnsGenericUnauthorized()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await LoginAsync(email, "WrongPassword123!");
        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsSameGenericUnauthorized()
    {
        var response = await LoginAsync(UniqueEmail());
        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsIdentity()
    {
        var email = UniqueEmail();
        var registration = await RegisterAsync(email);
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, json.GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task Login_NormalizesEmailCaseAndWhitespace()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await LoginAsync($"  {email.ToUpperInvariant()}  ");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, (await response.Content.ReadFromJsonAsync<AuthResponse>())?.Email);
    }

    private Task<HttpResponseMessage> RegisterAsync(string email, string password = "ValidPassword123!") =>
        _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password });

    private Task<HttpResponseMessage> LoginAsync(string email, string password = "ValidPassword123!") =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task AssertGenericUnauthorizedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Invalid email or password.", await response.Content.ReadAsStringAsync());
    }
}
