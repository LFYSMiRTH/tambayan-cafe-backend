using TambayanCafeSystem.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB
var connectionString = builder.Configuration["MongoDB:ConnectionString"]
                       ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
var databaseName = builder.Configuration["MongoDB:DatabaseName"]
                   ?? Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME");

if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("MongoDB ConnectionString missing.");
if (string.IsNullOrEmpty(databaseName))
    throw new InvalidOperationException("MongoDB DatabaseName missing.");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
});

builder.Services.AddSingleton<ProductService>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<UserService>();

// JSON
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ✅ CORRECT CORS SETUP
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL")?.Trim()
                  ?? "https://my-frontend-app-eight.vercel.app";

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendUrl)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition"); // optional
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ✅ Order matters: UseCors BEFORE UseRouting/MapControllers
app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.MapControllers();

// Health check
app.MapGet("/", () => "Tambayan Café API is live!");

// Port
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");