using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProductManagement.Application.Auth.Models;
using ProductManagement.Application.Products.Interfaces;
using ProductManagement.Application.Products.Models;
using ProductManagement.Tests.Auth;
using ProductManagement.Web.ExceptionHandling;

namespace ProductManagement.Tests.ExceptionHandling;

public sealed class ExceptionHandlingIntegrationTests
{
    [Fact]
    public async Task UnexpectedException_ReturnsSafeProblemDetailsAndIsLogged()
    {
        var logger = new CapturingLogger();
        await using var factory = new ThrowingProductFactory(logger);
        using var client = factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"exception-{Guid.NewGuid():N}@example.com",
            Password = "ValidPassword123!"
        });
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await client.GetAsync("/api/products");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("An unexpected error occurred.", body);
        Assert.Contains("traceId", body);
        Assert.DoesNotContain(ThrowingProductService.SensitiveMessage, body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(logger.Messages, message => message.Contains("Unhandled exception while processing"));
    }

    private sealed class ThrowingProductFactory(CapturingLogger logger) : AuthWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProductService>();
                services.AddScoped<IProductService, ThrowingProductService>();
                services.RemoveAll<ILogger<GlobalExceptionHandler>>();
                services.AddSingleton<ILogger<GlobalExceptionHandler>>(logger);
            });
        }
    }

    private sealed class ThrowingProductService : IProductService
    {
        public const string SensitiveMessage = "sensitive-database-path-and-secret";

        public Task<IReadOnlyList<ProductResponse>> GetProductsAsync(
            string? name,
            decimal? minPrice,
            decimal? maxPrice,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SensitiveMessage);

        public Task<ProductResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingLogger : ILogger<GlobalExceptionHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
