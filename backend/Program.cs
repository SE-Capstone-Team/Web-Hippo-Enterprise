using Data.Firestore;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;

// Minimal API host that fronts the Firestore-backed Hippo Exchange inventory system.

var builder = WebApplication.CreateBuilder(args);

// ===============================
// Firestore Configuration
// ===============================
// Prefer configuration, but fall back to environment variable so we can deploy to hosted envs
// without an appsettings file. The project ID is required; the app will not start without it.
var projectId = builder.Configuration["Firestore:ProjectId"] ??
                Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");

if (string.IsNullOrWhiteSpace(projectId))
{
    throw new InvalidOperationException("Firestore project ID is not configured. Set 'Firestore:ProjectId' in configuration or the 'GOOGLE_CLOUD_PROJECT' environment variable.");
}

var databaseId = builder.Configuration["Firestore:DatabaseId"] ??
                 Environment.GetEnvironmentVariable("FIRESTORE_DATABASE_ID") ??
                 "inventory-db";

// ✅ Create FirestoreDb first
var db = new FirestoreDbBuilder
{
    ProjectId = projectId,
    DatabaseId = databaseId
}.Build();

// ✅ Register Firestore and repositories
builder.Services.AddSingleton(db);
builder.Services.AddSingleton<FsProfiles>(sp => new FsProfiles(db));
builder.Services.AddSingleton<FsItems>(sp => new FsItems(db));
builder.Services.AddSingleton(_ => new FirestoreDbBuilder
{
    ProjectId = projectId,
    DatabaseId = databaseId

}.Build());

// ===============================
// CORS Configuration
// ===============================
const string DevCorsPolicy = "DevCors";
// CORS is hard-coded for local dev URLs
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:8000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// minimal APIs use System.Text.Json by default; configure it to use camelCaase to match JS conventions
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseCors(DevCorsPolicy);
// Binding the dev URL here so the app can be run without elevated permissions
app.Urls.Add("http://localhost:8000");

// ===============================
// Static Frontend Hosting
// ===============================
var frontendPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "src");

// Static file hosting points directly at the raw src directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
    RequestPath = ""
});

// ===============================
// USER ROUTES
// ===============================

// Create new user profile
app.MapPost("/api/users", async (UserProfile profile, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var created = await profiles.CreateAsync(profile, cancellationToken);
    return Results.Created($"/api/users/{created.OwnerId}", created);
});

// Get user by email (used for login)
app.MapGet("/api/users", async ([FromQuery] string? email, FsProfiles profiles, FirestoreDb db, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(email))
    {
        // Debug fallback returns the entire collection
        var collection = db.Collection("profiles");
        var snapshot = await collection.GetSnapshotAsync(cancellationToken);
        var allUsers = snapshot.Documents.Select(doc => doc.ConvertTo<UserProfile>()).ToList();
        return Results.Ok(allUsers);
    }

    // Firestore query runs client-side
    var profile = await profiles.FindByEmailAsync(email, cancellationToken);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

// Get user by ID
app.MapGet("/api/users/{ownerId}", async (string ownerId, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var profile = await profiles.ReadAsync(ownerId, cancellationToken);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

// Update user profile
app.MapPut("/api/users/{ownerId}", async (string ownerId, UserProfile profile, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    profile.OwnerId = ownerId;
    var updated = await profiles.UpdateAsync(profile, cancellationToken);
    return updated ? Results.NoContent() : Results.NotFound();
});

// Delete user
app.MapDelete("/api/users/{ownerId}", async (string ownerId, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var deleted = await profiles.DeleteAsync(ownerId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// List all items owned by a specific user
app.MapGet("/api/users/{ownerId}/items", async (string ownerId, FsItems items, CancellationToken cancellationToken) =>
{
    var list = await items.ListByOwnerAsync(ownerId, cancellationToken);
    return Results.Ok(list);
});

// List all items a user is currently borrowing
app.MapGet("/api/users/{ownerId}/borrowing", async (string ownerId, FsItems items, CancellationToken cancellationToken) =>
{
    var list = await items.ListByBorrowerAsync(ownerId, cancellationToken);
    return Results.Ok(list);
});

// ===============================
// ITEM ROUTES
// ===============================

// Create item
app.MapPost("/api/items", async (InventoryItem item, FsItems items, CancellationToken cancellationToken) =>
{
    var created = await items.CreateAsync(item, cancellationToken);
    return Results.Created($"/api/items/{created.ItemId}", created);
});

// Get all items or filter by OwnerId
app.MapGet("/api/items", async ([FromQuery] string? ownerId, FsItems items, CancellationToken cancellationToken) =>
{
    if (!string.IsNullOrWhiteSpace(ownerId))
    {
        var list = await items.ListByOwnerAsync(ownerId, cancellationToken);
        return Results.Ok(list);
    }

    var all = await items.ListAsync(cancellationToken);
    return Results.Ok(all);
});

// Get specific item by ID
app.MapGet("/api/items/{itemId}", async (string itemId, FsItems items, CancellationToken cancellationToken) =>
{
    var item = await items.ReadAsync(itemId, cancellationToken);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

// Update existing item
app.MapPut("/api/items/{itemId}", async (string itemId, InventoryItem item, FsItems items, CancellationToken cancellationToken) =>
{
    var existing = await items.ReadAsync(itemId, cancellationToken);
    if (existing is null)
    {
        return Results.NotFound();
    }

    // Manual field-by-field patching to avoid overwriting with defaults
    existing.OwnerId = string.IsNullOrWhiteSpace(item.OwnerId) ? existing.OwnerId : item.OwnerId;
    existing.Name = string.IsNullOrWhiteSpace(item.Name) ? existing.Name : item.Name;
    existing.Picture = string.IsNullOrWhiteSpace(item.Picture) ? existing.Picture : item.Picture;
    existing.Location = string.IsNullOrWhiteSpace(item.Location) ? existing.Location : item.Location;
    existing.Condition = string.IsNullOrWhiteSpace(item.Condition) ? existing.Condition : item.Condition;
    existing.PricePerDay = Math.Abs(item.PricePerDay) < double.Epsilon ? existing.PricePerDay : item.PricePerDay;
    existing.IsLent = item.IsLent;
    existing.BorrowerId = string.IsNullOrWhiteSpace(item.BorrowerId) ? existing.BorrowerId : item.BorrowerId;
    existing.BorrowedOn = item.BorrowedOn ?? existing.BorrowedOn;
    existing.DueAt = item.DueAt ?? existing.DueAt;

    if (!existing.IsLent)
    {
        existing.BorrowerId = string.Empty;
        existing.BorrowedOn = null;
        existing.DueAt = null;
    }

    var updated = await items.UpdateAsync(existing, cancellationToken);
    return updated ? Results.NoContent() : Results.Problem("Unable to update item.");
});

// Mark an item as borrowed
app.MapPost("/api/items/{itemId}/borrow", async (string itemId, BorrowRequest request, FsItems items, CancellationToken cancellationToken) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.BorrowerId))
    {
        return Results.BadRequest("BorrowerId is required.");
    }

    var item = await items.ReadAsync(itemId, cancellationToken);
    if (item is null)
    {
        return Results.NotFound();
    }

    if (item.IsLent)
    {
        return Results.Conflict("Item is already borrowed.");
    }

    item.IsLent = true;
    item.BorrowerId = request.BorrowerId;
    item.BorrowedOn = request.BorrowedOn ?? DateTime.UtcNow;
    item.DueAt = request.DueAt;

    var updated = await items.UpdateAsync(item, cancellationToken);
    return updated ? Results.Ok(item) : Results.Problem("Unable to mark item as borrowed.");
});

// Mark an item as returned
app.MapPost("/api/items/{itemId}/return", async (string itemId, FsItems items, CancellationToken cancellationToken) =>
{
    var item = await items.ReadAsync(itemId, cancellationToken);
    if (item is null)
    {
        return Results.NotFound();
    }

    if (!item.IsLent && string.IsNullOrWhiteSpace(item.BorrowerId))
    {
        return Results.Conflict("Item is not currently borrowed.");
    }

    item.IsLent = false;
    item.BorrowerId = string.Empty;
    item.BorrowedOn = null;
    item.DueAt = null;

    var updated = await items.UpdateAsync(item, cancellationToken);
    return updated ? Results.Ok(item) : Results.Problem("Unable to mark item as returned.");
});

// Delete an item
app.MapDelete("/api/items/{itemId}", async (string itemId, FsItems items, CancellationToken cancellationToken) =>
{
    // Fire-and-forget deletion
    var deleted = await items.DeleteAsync(itemId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// ===============================
// FRONTEND ROUTES
// ===============================
app.MapGet("/", () => Results.Content(
    "<html><body><h1>Welcome to Hippo Exchange!</h1></body></html>",
    "text/html"));

// Fallback: serve index.html for all non-API routes
app.MapFallback(async context =>
{
    var indexPath = Path.Combine(frontendPath, "index.html");
    if (File.Exists(indexPath))
    {
        // Serving raw HTML without cache headers
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
    }
});

app.Run();

internal sealed record BorrowRequest(string BorrowerId, DateTime? BorrowedOn, DateTime? DueAt);
