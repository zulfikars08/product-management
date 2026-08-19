using ProductManagement.Application.Products.Models;

namespace ProductManagement.Application.Products.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> GetProductsAsync(
        string? name,
        decimal? minPrice,
        decimal? maxPrice,
        CancellationToken cancellationToken = default);

    Task<ProductResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
