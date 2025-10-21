using Backend.Storage;
using Data.Firestore;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;
using Backend.Models;


// Minimal API host that fronts the Firestore-backed Hippo Exchange inventory system.

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton(_ => StorageClient.Create());
builder.Services.AddSingleton<FirebaseStorageService>();

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
builder.Services.AddSingleton<FsRequests>(sp => new FsRequests(db));
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
app.MapGet("/api/users/{ownerId}/items", async (string ownerId, FsItems items, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var list = await items.ListByOwnerAsync(ownerId, cancellationToken);
    var response = await InventoryItemMapper.ToViewListAsync(list, profiles, cancellationToken);
    return Results.Ok(response);
});

// List all items a user is currently borrowing
app.MapGet("/api/users/{ownerId}/borrowing", async (string ownerId, FsItems items, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var list = await items.ListByBorrowerAsync(ownerId, cancellationToken);
    var response = await InventoryItemMapper.ToViewListAsync(list, profiles, cancellationToken);
    return Results.Ok(response);
});

// ===============================
// ITEM ROUTES
// ===============================

app.MapPost("/api/uploads/items", async (HttpRequest request, FirebaseStorageService storage, CancellationToken cancellationToken) =>
{
    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("Image file is required.");
    }

    var ownerId = form.TryGetValue("ownerId", out var ownerValues)
        ? ownerValues.ToString()
        : null;

    try
    {
        var result = await storage.UploadItemImageAsync(file, ownerId, cancellationToken);
        return Results.Ok(new { url = result.Url, objectName = result.ObjectName });
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/uploads/profiles", async (HttpRequest request, FirebaseStorageService storage, CancellationToken cancellationToken) =>
{
    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("Profile image is required.");
    }

    var ownerHint = form.TryGetValue("ownerId", out var ownerValues)
        ? ownerValues.ToString()
        : null;

    try
    {
        var result = await storage.UploadProfileImageAsync(file, ownerHint, cancellationToken);
        return Results.Ok(new { url = result.Url, objectName = result.ObjectName });
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Create item
app.MapPost("/api/items", async (InventoryItemRequest item, FsItems items, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    if (item is null || string.IsNullOrWhiteSpace(item.OwnerId))
    {
        return Results.BadRequest("OwnerId is required.");
    }

    var created = await items.CreateAsync(item, cancellationToken);
    var response = await InventoryItemMapper.ToViewAsync(created, profiles, cancellationToken);
    return Results.Created($"/api/items/{created.ItemId}", response);
});

// Get all items or filter by OwnerId
app.MapGet("/api/items", async ([FromQuery] string? ownerId, FsItems items, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    if (!string.IsNullOrWhiteSpace(ownerId))
    {
        var list = await items.ListByOwnerAsync(ownerId, cancellationToken);
        var filtered = await InventoryItemMapper.ToViewListAsync(list, profiles, cancellationToken);
        return Results.Ok(filtered);
    }

    var all = await items.ListAsync(cancellationToken);
    var response = await InventoryItemMapper.ToViewListAsync(all, profiles, cancellationToken);
    return Results.Ok(response);
});

// Get specific item by ID
app.MapGet("/api/items/{itemId}", async (string itemId, FsItems items, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var item = await items.ReadAsync(itemId, cancellationToken);
    if (item is null)
    {
        return Results.NotFound();
    }

    var response = await InventoryItemMapper.ToViewAsync(item, profiles, cancellationToken);
    return Results.Ok(response);
});

// Update existing item
app.MapPut("/api/items/{itemId}", async (string itemId, InventoryItemRequest item, FsItems items, FirestoreDb db, CancellationToken cancellationToken) =>
{
    var existing = await items.ReadAsync(itemId, cancellationToken);
    if (existing is null)
    {
        return Results.NotFound();
    }

    // Manual field-by-field patching to avoid overwriting with defaults
    if (!string.IsNullOrWhiteSpace(item.OwnerId))
    {
        existing.OwnerRef = db.Collection("profiles").Document(item.OwnerId.Trim());
    }

    existing.Name = string.IsNullOrWhiteSpace(item.Name) ? existing.Name : item.Name;
    existing.Picture = string.IsNullOrWhiteSpace(item.Picture) ? existing.Picture : item.Picture;
    existing.Location = string.IsNullOrWhiteSpace(item.Location) ? existing.Location : item.Location;
    existing.Condition = string.IsNullOrWhiteSpace(item.Condition) ? existing.Condition : item.Condition;
    existing.PricePerDay = Math.Abs(item.PricePerDay) < double.Epsilon ? existing.PricePerDay : item.PricePerDay;
    existing.IsLent = item.IsLent;

    if (!string.IsNullOrWhiteSpace(item.BorrowerId))
    {
        existing.BorrowerRef = db.Collection("profiles").Document(item.BorrowerId.Trim());
    }

    existing.BorrowedOn = item.BorrowedOn ?? existing.BorrowedOn;
    existing.DueAt = item.DueAt ?? existing.DueAt;

    if (!existing.IsLent)
    {
        existing.BorrowerRef = null;
        existing.BorrowedOn = null;
        existing.DueAt = null;
    }

    var updated = await items.UpdateAsync(existing, cancellationToken);
    return updated ? Results.NoContent() : Results.Problem("Unable to update item.");
});

// Mark an item as borrowed
app.MapPost("/api/items/{itemId}/borrow", async (string itemId, BorrowRequest request, FsItems items, FsProfiles profiles, FirestoreDb db, CancellationToken cancellationToken) =>
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

    var borrowerRef = db.Collection("profiles").Document(request.BorrowerId.Trim());

    item.IsLent = true;
    item.BorrowerRef = borrowerRef;
    item.BorrowedOn = request.BorrowedOn ?? DateTime.UtcNow;
    item.DueAt = request.DueAt;

    var updated = await items.UpdateAsync(item, cancellationToken);
    if (!updated)
    {
        return Results.Problem("Unable to mark item as borrowed.");
    }

    var response = await InventoryItemMapper.ToViewAsync(item, profiles, cancellationToken);
    return Results.Ok(response);
});

// Mark an item as returned
app.MapPost("/api/items/{itemId}/return", async (string itemId, FsItems items) =>
{
    var success = await items.ReturnItemAsync(itemId);
    if (!success)
        return Results.NotFound(new { message = "Item not found or already available." });

    return Results.Ok(new { message = "Item returned successfully." });
});


// Delete an item
app.MapDelete("/api/items/{itemId}", async (string itemId, FsItems items, CancellationToken cancellationToken) =>
{
    // Fire-and-forget deletion
    var deleted = await items.DeleteAsync(itemId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// ===============================
// BORROW REQUEST ROUTES
// ===============================

// Create a borrow request (borrower asks owner)
app.MapPost("/api/requests", async (
    CreateRequestDto dto,
    FsItems items,
    FsRequests requests,
    FsProfiles profiles,
    FirestoreDb db,
    CancellationToken ct) =>
{
    if (dto is null || string.IsNullOrWhiteSpace(dto.ItemId) || string.IsNullOrWhiteSpace(dto.BorrowerId))
        return Results.BadRequest("ItemId and BorrowerId are required.");

    var item = await items.ReadAsync(dto.ItemId, ct);
    if (item is null) return Results.NotFound("Item not found.");

    var ownerId = item.OwnerRef?.Id ?? string.Empty;
    if (string.IsNullOrWhiteSpace(ownerId))
        return Results.BadRequest("Item has no owner.");

    if (ownerId == dto.BorrowerId)
        return Results.BadRequest("You cannot request your own item.");

    if (item.IsLent) return Results.Conflict("Item is currently loaned.");

    var entity = new BorrowRequestEntity
    {
        ItemId = item.ItemId,
        ItemName = item.Name,
        OwnerId = ownerId,
        BorrowerId = dto.BorrowerId,
        DueAt = dto.DueAt,
        Status = "pending"
    };

    var created = await requests.CreateAsync(entity, ct);
    return Results.Ok(created);
});

// Get pending requests for an owner (for bell dropdown)
app.MapGet("/api/requests/owner/{ownerId}", async (string ownerId, FsRequests requests, CancellationToken ct) =>
{
    var list = await requests.ListForOwnerAsync(ownerId, ct);
    return Results.Ok(list);
});

// Respond to a borrow request (accept or deny)
app.MapPost("/api/requests/{requestId}/respond", async (
    string requestId,
    RespondRequestDto body,
    FsRequests requests,
    FsItems items,
    FirestoreDb db,
    CancellationToken ct) =>
{
    var req = await requests.ReadAsync(requestId, ct);
    if (req is null) return Results.NotFound();

    if (!string.Equals(req.Status, "pending", StringComparison.OrdinalIgnoreCase))
        return Results.Conflict("Request is not pending.");

    if (!body.Accepted)
    {
        await requests.UpdateStatusAsync(requestId, "denied", ct);
        return Results.Ok(new { status = "denied" });
    }

    var item = await items.ReadAsync(req.ItemId, ct);
    if (item is null) return Results.NotFound("Item not found.");
    if (item.IsLent) return Results.Conflict("Item already loaned.");

    var borrowerRef = db.Collection("profiles").Document(req.BorrowerId);
    item.IsLent = true;
    item.BorrowerRef = borrowerRef;
    item.BorrowedOn = DateTime.UtcNow;
    item.DueAt = req.DueAt;

    var ok = await items.UpdateAsync(item, ct);
    if (!ok) return Results.Problem("Failed to update item.");

    await requests.UpdateStatusAsync(requestId, "accepted", ct);
    return Results.Ok(new { status = "accepted" });
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

