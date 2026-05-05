using HackerNews.Api.Extensions;
using HackerNews.Api.Middleware;
using HackerNews.Infrastructure;
using HackerNews.Application;

var builder = WebApplication.CreateBuilder(args);

// --- Service Registration ---

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Application layer (business services)
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddApplicationServices();

// Infrastructure layer (HttpClient, Polly, Cache, Settings)
builder.Services.AddInfrastructure(builder.Configuration);

// Response caching (optional layer on top of in-memory cache)
builder.Services.AddResponseCaching();

var app = builder.Build();

// --- Middleware Pipeline ---

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "HackerNews API v1"));
}

app.UseResponseCaching();
app.MapControllers();

app.Run();

// Required for integration tests with WebApplicationFactory<Program>
public partial class Program { }