using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ProductManagement.Application.Auth.Models;
using ProductManagement.Application.Products.Models;
using ProductManagement.Tests.Auth;

namespace ProductManagement.Tests.Security;

public sealed class SecurityRegressionTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>
{
    private const string SigningKey = "test-only-signing-key-at-least-32-characters-long";
    private readonly HttpClient _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Products_WithMalformedJwt_ReturnUnauthorizedInsteadOfServerError()
    {
        using var request = AuthorizedRequest("clearly.not-a-valid.jwt");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Products_WithValidlySignedExpiredJwt_ReturnUnauthorized()
    {
        var now = DateTime.UtcNow;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "ProductManagement.Tests",
            audience: "ProductManagement.Tests.Client",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            notBefore: now.AddHours(-2),
            expires: now.AddHours(-1),
            signingCredentials: credentials);
        using var request = AuthorizedRequest(new JwtSecurityTokenHandler().WriteToken(token));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_IgnoresClientSuppliedCreatedAtAndUsesServerTime()
    {
        var token = await RegisterAndGetTokenAsync();
        var clientCreatedAt = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new
            {
                name = $"Mass assignment check {Guid.NewGuid():N}",
                description = "CreatedAt must remain server-owned.",
                price = 25,
                createdAt = clientCreatedAt
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var before = DateTime.UtcNow;

        var response = await _client.SendAsync(request);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(product);
        Assert.NotEqual(clientCreatedAt, product.CreatedAt);
        Assert.InRange(product.CreatedAt, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task PublicAuthResponses_NeverSerializePasswordHash()
    {
        var email = $"serialization-{Guid.NewGuid():N}@example.com";
        var registration = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "ValidPassword123!"
        });
        var registrationJson = await registration.Content.ReadAsStringAsync();
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "ValidPassword123!"
        });
        var loginJson = await login.Content.ReadAsStringAsync();

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        var me = await _client.SendAsync(meRequest);
        var meJson = await me.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        AssertDoesNotContainPasswordHash(registrationJson);
        AssertDoesNotContainPasswordHash(loginJson);
        AssertDoesNotContainPasswordHash(meJson);
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"security-{Guid.NewGuid():N}@example.com",
            Password = "ValidPassword123!"
        });
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage AuthorizedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static void AssertDoesNotContainPasswordHash(string json)
    {
        Assert.DoesNotContain("PasswordHash", json, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", json, StringComparison.Ordinal);
    }
}
