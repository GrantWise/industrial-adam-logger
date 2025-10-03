# Multi-stage build for Industrial ADAM Logger
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Industrial.Adam.Logger.sln ./
COPY Directory.Build.props ./
COPY src/Industrial.Adam.Logger.Core/Industrial.Adam.Logger.Core.csproj ./src/Industrial.Adam.Logger.Core/
COPY src/Industrial.Adam.Logger.WebApi/Industrial.Adam.Logger.WebApi.csproj ./src/Industrial.Adam.Logger.WebApi/

# Restore dependencies
RUN dotnet restore Industrial.Adam.Logger.sln

# Copy source code
COPY src/ ./src/

# Build and publish
WORKDIR /src/src/Industrial.Adam.Logger.WebApi
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Create non-root user
RUN useradd -m -u 1000 adamlogger && chown -R adamlogger:adamlogger /app
USER adamlogger

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

# Start application
ENTRYPOINT ["dotnet", "Industrial.Adam.Logger.WebApi.dll"]
