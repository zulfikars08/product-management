using System.ComponentModel.DataAnnotations;

namespace ProductManagement.Application.Products.Models;

public sealed class CreateProductRequest : IValidatableObject
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Price { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Name must not be blank.", [nameof(Name)]);
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            yield return new ValidationResult("Description must not be blank.", [nameof(Description)]);
        }
    }
}
