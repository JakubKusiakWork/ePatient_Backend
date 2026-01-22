using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Playwright;
using PharmacyChecker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("backend", client =>
{
    var cfg = builder.Configuration.GetValue<string>("BackendApiUrl") ?? "http://localhost:5000";
    client.BaseAddress = new Uri(cfg);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<ConfigLoader>();
builder.Services.AddSingleton<PharmacyScanner>();
builder.Services.AddSingleton<ChangeDetector>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapPost("/api/scrape", async (HttpContext context, PharmacyScanner scanner, ConfigLoader configLoader, IConfiguration config) =>
{
    try
    {
        var request = await context.Request.ReadFromJsonAsync<ScrapeRequest>();
        if (request == null || string.IsNullOrWhiteSpace(request.Product) || string.IsNullOrWhiteSpace(request.Location))
        {
            return Results.BadRequest(new { error = "Product and Location are required" });
        }

        var backendUrl = config.GetValue<string>("BackendApiUrl") ?? "http://localhost:5000";
        var result = await scanner.CheckSingleProductAsync(request.Product, request.Location, backendUrl, configLoader);
        
        return Results.Ok(new { success = true, product = request.Product, result });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();

public record ScrapeRequest(string Product, string Location);
