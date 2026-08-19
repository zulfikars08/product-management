namespace ProductManagement.Application.Auth.Models;

public enum AuthFailure
{
    None,
    DuplicateEmail,
    InvalidCredentials
}

public sealed record AuthResult(AuthResponse? Response, AuthFailure Failure)
{
    public static AuthResult Success(AuthResponse response) => new(response, AuthFailure.None);
    public static AuthResult Failed(AuthFailure failure) => new(null, failure);
}
