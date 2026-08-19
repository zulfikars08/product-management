using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).IsRequired();
        builder.Property(user => user.PasswordHash).IsRequired();
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();
    }
}
