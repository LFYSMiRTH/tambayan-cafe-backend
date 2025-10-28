# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY *.csproj .
RUN dotnet restore

# Copy all source code
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Use the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose port (Render will override with PORT env var)
EXPOSE 8080

# Run the app
ENTRYPOINT ["dotnet", "TambayanCafeAPI.dll"]