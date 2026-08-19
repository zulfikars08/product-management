using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProductManagement.Application.Auth.Models;
using ProductManagement.Application.Products.Models;
using ProductManagement.Tests.Auth;

namespace ProductManagement.Tests.Products;

public sealed class ProductIntegrationTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    private string _token = string.Empty;

    public async Task InitializeAsync()
    {
        var email = $"product-tests-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "ValidPassword123!"
        });
        _token = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetProducts_WithoutJwt_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/products")).StatusCode);
    }

    [Fact]
    public async Task PostProduct_WithoutJwt_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/products", ValidCreate())).StatusCode);
    }

    [Fact]
    public async Task Create_ValidProduct_ReturnsCreatedWithServerCreatedAt()
    {
        var before = DateTime.UtcNow;
        var response = await CreateAsync();
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(product);
        Assert.InRange(product.CreatedAt, before, DateTime.UtcNow);
        Assert.True(product.Id > 0);
    }

    [Theory]
    [InlineData("", "Description", 10)]
    [InlineData("   ", "Description", 10)]
    [InlineData("Name", "   ", 10)]
    [InlineData("Name", "Description", 0)]
    [InlineData("Name", "Description", -1)]
    public async Task Create_InvalidInput_ReturnsBadRequest(string name, string description, decimal price)
    {
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = name,
            Description = description,
            Price = price
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingAndUnknown_MapCorrectly()
    {
        var created = await CreatedProductAsync();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/products/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/products/2147483647")).StatusCode);
    }

    [Fact]
    public async Task Update_Existing_PreservesCreatedAt()
    {
        var created = await CreatedProductAsync();
        var response = await _client.PutAsJsonAsync($"/api/products/{created.Id}", new UpdateProductRequest
        {
            Name = " Updated ",
            Description = " Updated description ",
            Price = 99
        });
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.CreatedAt, updated!.CreatedAt);
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync("/api/products/2147483647", new UpdateProductRequest
        {
            Name = "Name",
            Description = "Description",
            Price = 1
        })).StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingThenGet_ReturnsNoContentThenNotFound()
    {
        var created = await CreatedProductAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/products/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/products/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_Unknown_ReturnsNotFound()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/products/2147483647")).StatusCode);
    }

    [Fact]
    public async Task NameSearch_IsPartialAndCaseInsensitive()
    {
        var marker = Guid.NewGuid().ToString("N");
        await CreatedProductAsync($"Wireless PHONE {marker}", 250);
        await CreatedProductAsync($"Keyboard {marker}", 750);

        var lower = await GetProductsAsync($"?name=phone%20{marker}");
        var upper = await GetProductsAsync($"?name=PHONE%20{marker}");
        Assert.Single(lower);
        Assert.Single(upper);
        Assert.Equal(lower[0].Id, upper[0].Id);
    }

    [Fact]
    public async Task PriceBounds_AreInclusiveAndSupportSingleBounds()
    {
        var marker = Guid.NewGuid().ToString("N");
        await CreatedProductAsync(marker, 100);
        await CreatedProductAsync(marker, 200);
        await CreatedProductAsync(marker, 300);

        Assert.Equal(2, (await GetProductsAsync($"?name={marker}&minPrice=200")).Count);
        Assert.Equal(2, (await GetProductsAsync($"?name={marker}&maxPrice=200")).Count);
        Assert.Single(await GetProductsAsync($"?name={marker}&minPrice=200&maxPrice=200"));
    }

    [Fact]
    public async Task CombinedNameAndPriceFilters_ApplyTogether()
    {
        var marker = Guid.NewGuid().ToString("N");
        await CreatedProductAsync($"Mouse {marker}", 250);
        await CreatedProductAsync($"Mouse {marker}", 500);
        await CreatedProductAsync($"Keyboard {marker}", 250);

        var products = await GetProductsAsync($"?name=mouse%20{marker}&minPrice=200&maxPrice=300");
        Assert.Single(products);
        Assert.Equal(250, products[0].Price);
    }

    [Theory]
    [InlineData("?minPrice=-1")]
    [InlineData("?maxPrice=-1")]
    [InlineData("?minPrice=500&maxPrice=100")]
    public async Task InvalidPriceRange_ReturnsBadRequest(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/products{query}")).StatusCode);
    }

    private Task<HttpResponseMessage> CreateAsync() => _client.PostAsJsonAsync("/api/products", ValidCreate());

    private async Task<ProductResponse> CreatedProductAsync(string? name = null, decimal price = 10)
    {
        var response = await _client.PostAsJsonAsync("/api/products", ValidCreate(name, price));
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private async Task<List<ProductResponse>> GetProductsAsync(string query) =>
        (await _client.GetFromJsonAsync<List<ProductResponse>>($"/api/products{query}"))!;

    private static CreateProductRequest ValidCreate(string? name = null, decimal price = 10) => new()
    {
        Name = name ?? $"Product {Guid.NewGuid():N}",
        Description = "Description",
        Price = price
    };
}
