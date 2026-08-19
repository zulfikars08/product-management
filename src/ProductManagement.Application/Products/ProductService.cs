using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products.Interfaces;
using ProductManagement.Application.Products.Models;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed class ProductService(IApplicationDbContext dbContext) : IProductService
{
    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(
        string? name,
        decimal? minPrice,
        decimal? maxPrice,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products.AsNoTracking();
        var trimmedName = name?.Trim();

        if (!string.IsNullOrWhiteSpace(trimmedName))
        {
            var pattern = $"%{EscapeLikePattern(trimmedName.ToLowerInvariant())}%";
            query = query.Where(product => EF.Functions.Like(product.Name.ToLower(), pattern, "\\"));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(product => product.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(product => product.Price <= maxPrice.Value);
        }

        return await query
            .OrderByDescending(product => product.CreatedAt)
            .ThenByDescending(product => product.Id)
            .Select(product => ToResponse(product))
            .ToListAsync(cancellationToken);
    }

    public Task<ProductResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == id)
            .Select(product => ToResponse(product))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Price = request.Price,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(product);
    }

    public async Task<ProductResponse?> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (product is null)
        {
            return null;
        }

        product.Name = request.Name.Trim();
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(product);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (product is null)
        {
            return false;
        }

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ProductResponse ToResponse(Product product) =>
        new(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            DateTime.SpecifyKind(product.CreatedAt, DateTimeKind.Utc));

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
