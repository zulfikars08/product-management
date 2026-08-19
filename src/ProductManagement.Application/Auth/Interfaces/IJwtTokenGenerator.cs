using ProductManagement.Application.Auth.Models;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Auth.Interfaces;

public interface IJwtTokenGenerator
{
    AuthResponse Generate(User user);
}
