namespace ProductManagement.Application.Products.Models;

public sealed record ProductResponse(
    int Id,
    string Name,
    string Description,
    decimal Price,
    DateTime CreatedAt);
