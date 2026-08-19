# Product Management

Product Management is a production-minded ASP.NET Core application for registering users and managing a protected product catalog. It provides a JWT-secured REST API plus a simple Razor/MVC frontend that communicates with the same HTTP endpoints.

The project favors a small layered architecture, portable SQLite persistence, safe validation and error responses, structured logging, and executable integration tests.

## Features

- JWT registration, login, session verification, and protected Product endpoints
- Product create, list, detail, update, and delete operations
- Partial case-insensitive name search and inclusive price-range filtering
- Data Annotation and business validation with appropriate HTTP responses
- EF Core SQLite persistence with automatic migration on startup
- Structured Serilog console and HTTP request logging
- Safe RFC-style ProblemDetails responses for unexpected exceptions
- Razor/MVC, Bootstrap, and vanilla JavaScript frontend using the HTTP API
- Automated authentication, authorization, Product, validation, exception, and frontend integration tests

## Tech Stack

- .NET 10 / ASP.NET Core Web API and MVC
- Entity Framework Core 10 and SQLite
- JWT Bearer Authentication and Microsoft PasswordHasher
- Serilog with console and request logging
- Razor/MVC, Bootstrap, and vanilla JavaScript
- xUnit and Microsoft.AspNetCore.Mvc.Testing

## Architecture

- **Domain** — Product and User entities; no Infrastructure or Web dependency.
- **Application** — request/response models, service contracts, persistence abstraction, and Product application logic.
- **Infrastructure** — EF Core SQLite persistence, migrations, password hashing, authentication, and JWT generation.
- **Web** — composition root, API controllers, Razor frontend, JavaScript API client, logging, and global exception handling.
- **Tests** — functional/integration coverage using isolated temporary SQLite databases.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

Verified with .NET SDK `10.0.400`.

## Local Setup

1. Clone the repository and enter it:
   ```bash
   git clone https://github.com/zulfikars08/product-management.git
   cd product-management
   ```
2. Set a process-local JWT signing key containing at least 32 characters:
   ```powershell
   # Windows PowerShell
   $env:Jwt__Key="<YOUR-32+-CHARACTER-SECRET>"
   ```
   ```bash
   # Linux/macOS
   export Jwt__Key="<YOUR-32+-CHARACTER-SECRET>"
   ```
3. Restore and build:
   ```bash
   dotnet restore
   dotnet build
   ```
4. Run the Web project:
   ```bash
   dotnet run --project src/ProductManagement.Web
   ```
   EF Core migrations run automatically and create the local SQLite database on first startup. No manual database setup is required.
5. Open the localhost URL printed in the startup console, then use the **Register** tab to create an account and enter the Product Management UI.

## Usage

Open the application root, register an account (or sign in), then add, edit, search, filter, and delete Products. The browser frontend stores the issued JWT only for the current tab session and sends all Product operations through the protected REST API.

## API Summary

| Method | Endpoint | Description | Authentication |
|---|---|---|---|
| POST | `/api/auth/register` | Register and receive a JWT | No |
| POST | `/api/auth/login` | Sign in and receive a JWT | No |
| GET | `/api/auth/me` | Verify current identity | Bearer JWT |
| GET | `/api/products` | List/search/filter Products | Bearer JWT |
| GET | `/api/products/{id}` | Get Product by ID | Bearer JWT |
| POST | `/api/products` | Create Product | Bearer JWT |
| PUT | `/api/products/{id}` | Update Product | Bearer JWT |
| DELETE | `/api/products/{id}` | Delete Product | Bearer JWT |

Protected requests use:

```http
Authorization: Bearer <token>
```

Search/filter examples:

```http
GET /api/products?name=phone
GET /api/products?minPrice=100&maxPrice=500
GET /api/products?name=phone&minPrice=100&maxPrice=500
```

## Assumptions

- User email is trimmed and normalized to lowercase invariant.
- Password length is 8–128 characters.
- Product Name is required with a maximum length of 200 characters.
- Product Description is required with a maximum length of 2000 characters.
- Product Price must be greater than zero.
- `CreatedAt` is assigned server-side in UTC and preserved during update.
- Name search is partial and case-insensitive for supported normal Product names.
- Price bounds are inclusive, and minimum price cannot exceed maximum price.
- JWT lifetime is 60 minutes; its signing key comes from `Jwt__Key`.
- All Product endpoints require authentication.
- SQLite is used for portable, zero-configuration local persistence.
- The simple frontend keeps its JWT in browser `sessionStorage`.

## Security Notes

- No production JWT signing key is committed; startup rejects missing or short keys.
- Passwords are hashed with Microsoft `PasswordHasher<User>` and never stored in plaintext.
- `PasswordHash` is never returned by the API, and the frontend never stores passwords.
- Invalid login responses do not reveal whether an email exists.
- Unexpected failures return generic ProblemDetails with a trace identifier, not stack traces or internal exception details.
- `sessionStorage` is a pragmatic short-lived token choice for this assessment UI; closing the tab ends that browser session.

## Testing

Run the complete suite with:

```bash
dotnet test
```

The suite covers authentication, authorization, Product CRUD, validation, search/filter behavior, exception handling, and frontend/static integration.

## Project Structure

```text
src/
├── ProductManagement.Domain/
├── ProductManagement.Application/
├── ProductManagement.Infrastructure/
└── ProductManagement.Web/
tests/
└── ProductManagement.Tests/
```
