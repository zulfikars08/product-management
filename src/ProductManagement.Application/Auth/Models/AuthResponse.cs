namespace ProductManagement.Application.Auth.Models;

public sealed record AuthResponse(string Token, DateTime ExpiresAt, string Email);
