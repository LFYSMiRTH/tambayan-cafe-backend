using MongoDB.Bson;
using MongoDB.Driver;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Logging;

System.Net.ServicePointManager.SecurityProtocol =
    System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;

var builder = WebApplication.CreateBuilder(args);

// 🔐 JWT Authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
                     ?? builder.Configuration["Jwt:Key"]
                     ?? "ThisIsYourVerySecureSecretKey123!@#";
        var key = Encoding.UTF8.GetBytes(jwtKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TambayanCafeAPI",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TambayanCafeClient",
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero, // Optional: strict expiration
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration["MongoDB:ConnectionString"]
                       ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
var databaseName = builder.Configuration["MongoDB:DatabaseName"]
                   ?? Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME");

if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("MongoDB ConnectionString is missing.");
if (string.IsNullOrEmpty(databaseName))
    throw new InvalidOperationException("MongoDB DatabaseName is missing.");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
});

// ✅ Register services with logging — ORDER MATTERS!
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<SupplierService>();

// 🔥 NEW: Register NotificationService BEFORE dependent services
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ReorderService>();
builder.Services.AddScoped<OrderService>(); // Now depends on NotificationService

// 🔥 Keep interfaces for DI
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IMenuItemService, ProductService>();

builder.Services.AddSingleton<IReportService>(sp =>
{
    var orderService = sp.GetRequiredService<OrderService>();
    var inventoryService = sp.GetRequiredService<InventoryService>();
    var productService = sp.GetRequiredService<ProductService>();
    var database = sp.GetRequiredService<IMongoDatabase>();
    return new ReportService(orderService, inventoryService, productService, database);
});

// 👈 ADDED: Register concrete ReportService for direct injection (e.g., in DashboardController)
builder.Services.AddScoped<ReportService>();

// 🔥 Background service (still after scoped services)
builder.Services.AddHostedService<ReorderBackgroundService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ✅ CORRECTED CORS (NO TRAILING SPACES!)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            var cleanOrigin = origin?.Trim();
            if (string.Equals(cleanOrigin, "https://my-frontend-app-eight.vercel.app        ", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(cleanOrigin) &&
                cleanOrigin.StartsWith("https://my-frontend-app-", StringComparison.OrdinalIgnoreCase) &&
                cleanOrigin.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                return true;
            var localOrigins = new[]
            {
                "http://127.0.0.1:5500",
                "http://localhost:5500",
                "http://localhost:3000",
                "http://127.0.0.1:3000"
            };
            return localOrigins.Contains(cleanOrigin);
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials(); // Optional but safe
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Global error handler
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex}");
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = "Internal server error" })
        );
    }
});

app.MapControllers();

// Health endpoints
app.MapGet("/health", () => "Tambayan Café API is live!");
app.MapGet("/health/db", async (IMongoDatabase database) =>
{
    try
    {
        await database.RunCommandAsync((Command<BsonDocument>)"{ ping: 1 }");
        return Results.Ok(new { status = "MongoDB connected!" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");