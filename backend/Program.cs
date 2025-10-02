using Data.Firestore;
using Google.Cloud.Firestore;

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

builder.Services.AddSingleton(_ =>
{
    var fb = new FirestoreDbBuilder
    {
        ProjectId = projectId,
        DatabaseId = databaseId
    };
    return fb.Build();
});

var MyCors = "_dev";
builder.Services.AddCors(policy =>
{
    policy.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});
var app = builder.Build();
app.UseCors(MyCors);

app.Urls.Add("http://localhost:8000");

// Configure static file serving for the frontend
var frontendPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "src");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
    RequestPath = ""
});

// API routes
app.MapPost("/api/users", async (UserProfile profile, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var created = await profiles.CreateAsync(profile, cancellationToken);
    return Results.Created($"/api/users/{created.UserId}", created);
});

app.MapGet("/api/users/{userId}", async (string userId, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var profile = await profiles.ReadAsync(userId, cancellationToken);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});
app.MapGet("/api/users/{userId}/items", async (string userId, FirestoreDb db, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest("UserId cannot be null or empty.");
    }
    var collection = db.Collection("items");
    var query = collection.WhereEqualTo("ownerId", userId);
    var snapshot = await query.GetSnapshotAsync(cancellationToken);
    var ownedItems = snapshot.Documents.Select(doc => doc.ConvertTo<InventoryItem>()).ToList();
    return Results.Ok(ownedItems);
});

app.MapPut("/api/users/{userId}", async (string userId, UserProfile profile, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    profile.UserId = userId;
    var updated = await profiles.UpdateAsync(profile, cancellationToken);
    return updated ? Results.NoContent() : Results.NotFound();
});

app.MapDelete("/api/users/{userId}", async (string userId, FsProfiles profiles, CancellationToken cancellationToken) =>
{
    var deleted = await profiles.DeleteAsync(userId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/api/items", async (InventoryItem item, FsItems items, CancellationToken cancellationToken) =>
{
    var created = await items.CreateAsync(item, cancellationToken);
    return Results.Created($"/api/items/{created.ItemId}", created);
});

app.MapGet("/api/items/{itemId}", async (string itemId, FsItems items, CancellationToken cancellationToken) =>
{
    var item = await items.ReadAsync(itemId, cancellationToken);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPut("/api/items/{itemId}", async (string itemId, InventoryItem item, FsItems items, CancellationToken cancellationToken) =>
{
    item.ItemId = itemId;
    var updated = await items.UpdateAsync(item, cancellationToken);
    return updated ? Results.NoContent() : Results.NotFound();
});

app.MapDelete("/api/items/{itemId}", async (string itemId, FsItems items, CancellationToken cancellationToken) =>
{
    var deleted = await items.DeleteAsync(itemId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// Serve index.html for the root route and handle SPA routing
app.MapFallback(async context =>
{
    
    // For root or any non-API route, serve index.html
    var indexPath = Path.Combine(frontendPath, "index.html");
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
    }
});

app.Run();
