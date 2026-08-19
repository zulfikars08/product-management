using ProductManagement.Infrastructure;
using ProductManagement.Infrastructure.Persistence;
using ProductManagement.Web.ExceptionHandling;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.ApplyDatabaseMigrationsAsync();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program;
