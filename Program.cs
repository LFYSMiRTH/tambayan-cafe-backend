using MongoDB.Bson;
using MongoDB.Driver;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services;

System.Net.ServicePointManager.SecurityProtocol =
    System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;

var builder = WebApplication.CreateBuilder(args);

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

// ✅ Register core services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IMenuItemService, ProductService>();

// ❌ REMOVED: builder.Services.AddSingleton<ProductService>(); — avoid duplicate instances

// Keep other singletons that are manually constructed
builder.Services.AddSingleton<InventoryService>();
builder.Services.AddSingleton<SupplierService>();

builder.Services.AddSingleton<OrderService>(sp =>
{
    var db = sp.GetRequiredService<IMongoDatabase>();
    var productService = sp.GetRequiredService<IMenuItemService>() as ProductService;
    var inventoryService = sp.GetRequiredService<InventoryService>();
    return new OrderService(db, productService, inventoryService);
});

builder.Services.AddSingleton<IReportService>(sp =>
{
    var orderService = sp.GetRequiredService<OrderService>();
    var inventoryService = sp.GetRequiredService<InventoryService>();
    var productService = sp.GetRequiredService<IMenuItemService>() as ProductService;
    var database = sp.GetRequiredService<IMongoDatabase>();
    return new ReportService(orderService, inventoryService, productService, database);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            var cleanOrigin = origin?.Trim();
            if (string.Equals(cleanOrigin, "https://my-frontend-app-eight.vercel.app", StringComparison.OrdinalIgnoreCase))
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
        .AllowAnyMethod();
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

// ✅ SIMPLE AUTH MIDDLEWARE: Accept Bearer <user-id>
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var userId = authHeader["Bearer ".Length..].Trim();
        if (!string.IsNullOrEmpty(userId))
        {
            var claims = new[]
            {
                new System.Security.Claims.Claim("id", userId),
                new System.Security.Claims.Claim("role", "customer")
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "api-key");
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }
    }
    await next();
});

// Global error handler (keep as is)
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