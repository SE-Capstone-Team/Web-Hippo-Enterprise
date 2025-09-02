/* 

The following is the example skeleton for our app's webapi.

*/ 

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Urls.Add("http://localhost:8000");

app.MapPost("/api/users/register", (CredentialsRecord credentialsRecord) =>
{
    // Hash password
    // Call database function to create user with email and hashed password
    // Return success or failure response
});

app.MapPost("/api/auth/login", (CredentialsRecord credentialsRecord) =>
{
    // Hash password
    // Retrieve stored hash from database using credentialsRecord.Email
    // Compare hashedPassword to stored hash
    // Return success or failure response
});

app.MapGet("/api/users/{username}", (String username) =>
{
    // Retrieve user information from database using username
    // Return user information or error response
});

app.MapPost("/api/asset", (AssetRecord assetRecord) =>
{
    // Call database function to create asset with assetRecord.Username, assetRecord.Name, and assetRecord.Description
    // Return success or failure response
});

app.MapGet("/", () =>
{
    // Return landing page
    return Results.Content(
        "<html><body><h1>Welcome to Hippo Exchange!</h1></body></html>",
        "text/html");
});

app.Run();

public record CredentialsRecord(string Email, string Password);
public record AssetRecord(string Username, string Name, string Description);