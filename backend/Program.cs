using Data.Firestore;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var projectId = builder.Configuration["Firestore:ProjectId"] ??
                Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
if (string.IsNullOrWhiteSpace(projectId))
{
    throw new InvalidOperationException("Firestore project ID is not configured. Set 'Firestore:ProjectId' in configuration or the 'GOOGLE_CLOUD_PROJECT' environment variable.");
}

var databaseId = builder.Configuration["Firestore:DatabaseId"] ??
                 Environment.GetEnvironmentVariable("FIRESTORE_DATABASE_ID") ??
                 "inventory-db";

builder.Services.AddSingleton<FsProfiles>();
builder.Services.AddSingleton<FsItems>();
builder.Services.AddSingleton(_ => new FirestoreDbBuilder
{
    ProjectId = projectId,
    DatabaseId = databaseId
}.Build());

const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseCors(DevCorsPolicy);
app.Urls.Add("http://localhost:8000");

var frontendPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "src");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
    RequestPath = ""
});

app.MapPost("/api/users", async (UserProfile profile, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var created = await profiles.CreateAsync(profile, cancellationToken);
    return Results.Created($"/api/users/{created.UserId}", created);
});

app.MapGet("/api/users", async ([FromQuery] string? email, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(email))
    {
        return Results.BadRequest("Email query parameter is required.");
    }

    var profile = await profiles.FindByEmailAsync(email, cancellationToken);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

app.MapGet("/api/users/{ownerUserId}", async (string ownerUserId, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var profile = await profiles.ReadAsync(ownerUserId, cancellationToken);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

app.MapPut("/api/users/{ownerUserId}", async (string ownerUserId, UserProfile profile, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    profile.UserId = ownerUserId;
    var updated = await profiles.UpdateAsync(profile, cancellationToken);
    return updated ? Results.NoContent() : Results.NotFound();
});

app.MapDelete("/api/users/{ownerUserId}", async (string ownerUserId, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var deleted = await profiles.DeleteAsync(ownerUserId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/api/users/{ownerUserId}/items", async (string ownerUserId, [FromServices] FsItems items, CancellationToken cancellationToken) =>
{
    var list = await items.ListByOwnerAsync(ownerUserId, cancellationToken);
    return Results.Ok(list);
});

app.MapPost("/api/items", async (InventoryItem item, FsItems items, CancellationToken cancellationToken) =>
{
    var created = await items.CreateAsync(item, cancellationToken);
    return Results.Created($"/api/items/{created.ItemId}", created);
});

app.MapGet("/api/items", async ([FromQuery] string? ownerUserId, FsItems items, CancellationToken cancellationToken) =>
{
    if (!string.IsNullOrWhiteSpace(ownerUserId))
    {
        var list = await items.ListByOwnerAsync(ownerUserId, cancellationToken);
        return Results.Ok(list);
    }

    var all = await items.ListAsync(cancellationToken);
    return Results.Ok(all);
});

app.MapGet("/api/items/{itemId}", async (string itemId, FsItems items, CancellationToken cancellationToken) =>
{
    var item = await items.ReadAsync(itemId, cancellationToken);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPut("/api/items/{itemId}", async (string itemId, InventoryItem item, FsItems items, CancellationToken cancellationToken) =>
{
    var existing = await items.ReadAsync(itemId, cancellationToken);
    if (existing is null)
    {
        return Results.NotFound();
    }

    item.ItemId = itemId;
    item.OwnerUserId = string.IsNullOrWhiteSpace(item.OwnerUserId) ? existing.OwnerUserId : item.OwnerUserId;
    item.Name = string.IsNullOrWhiteSpace(item.Name) ? existing.Name : item.Name;
    item.Picture = string.IsNullOrWhiteSpace(item.Picture) ? existing.Picture : item.Picture;
    item.Location = string.IsNullOrWhiteSpace(item.Location) ? existing.Location : item.Location;
    item.Condition = string.IsNullOrWhiteSpace(item.Condition) ? existing.Condition : item.Condition;
    item.Status = string.IsNullOrWhiteSpace(item.Status) ? existing.Status : item.Status;
    item.PricePerDay = Math.Abs(item.PricePerDay) < double.Epsilon ? existing.PricePerDay : item.PricePerDay;

    var updated = await items.UpdateAsync(item, cancellationToken);
    return updated ? Results.NoContent() : Results.Problem("Unable to update item.");
});

app.MapDelete("/api/items/{itemId}", async (string itemId, FsItems items, CancellationToken cancellationToken) =>
{
    var deleted = await items.DeleteAsync(itemId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/", () => Results.Content(
    "<html><body><h1>Welcome to Hippo Exchange!</h1></body></html>",
    "text/html"));

app.MapFallback(async context =>
{
    var indexPath = Path.Combine(frontendPath, "index.html");
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
    }
});

app.Run();
