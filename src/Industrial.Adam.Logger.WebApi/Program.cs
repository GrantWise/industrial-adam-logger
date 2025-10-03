using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Industrial.Adam.Logger.Core.Devices;
using Industrial.Adam.Logger.Core.Extensions;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Services;
using Industrial.Adam.Logger.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add environment file support
builder.Configuration.AddEnvironmentFiles(builder.Environment.EnvironmentName);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ADAM Industrial Logger API",
        Version = "v1",
        Description = "Minimal API for ADAM-6051 device monitoring and data collection",
        Contact = new()
        {
            Name = "Industrial Systems Team",
            Email = "support@industrialsystems.com"
        }
    });

    // JWT Authentication configuration
    c.AddSecurityDefinition("Bearer", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT Bearer token to access protected endpoints"
    });

    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments
    var xmlPath = Path.Combine(AppContext.BaseDirectory, "Industrial.Adam.Logger.WebApi.xml");
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add simple JWT authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is required");
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("Production", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add ADAM Logger Core services
builder.Services.AddAdamLogger(builder.Configuration);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<TimescaleDbHealthCheck>("timescaledb")
    .AddCheck<DevicePoolHealthCheck>("device-pool");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADAM Logger API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

// Use CORS policy
app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

// Add authentication and authorization
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

// Cache for latest readings
var latestReadings = new ConcurrentDictionary<string, DeviceReading>();

// Subscribe to device readings on startup
var devicePool = app.Services.GetRequiredService<IServiceProvider>().GetService<ModbusDevicePool>();
if (devicePool != null)
{
    devicePool.ReadingReceived += (reading) =>
    {
        var key = $"{reading.DeviceId}:{reading.Channel}";
        latestReadings.AddOrUpdate(key, reading, (k, existing) => reading);
    };
}

// ============================================================================
// HEALTH ENDPOINTS
// ============================================================================

app.MapGet("/health", (AdamLoggerService loggerService) =>
{
    var status = loggerService.GetStatus();
    var result = new
    {
        Status = status.IsRunning ? "Healthy" : "Unhealthy",
        Timestamp = DateTimeOffset.UtcNow,
        Service = new
        {
            IsRunning = status.IsRunning,
            StartTime = status.StartTime,
            Uptime = status.IsRunning ? DateTimeOffset.UtcNow - status.StartTime : TimeSpan.Zero
        },
        Devices = new
        {
            Total = status.TotalDevices,
            Connected = status.ConnectedDevices,
            Health = status.DeviceHealth
        }
    };

    return Results.Ok(result);
})
.WithName("GetHealth")
.WithSummary("Get service health status")
.WithDescription("Returns overall health status of the ADAM Logger service including device connectivity")
.Produces<object>(200)
.Produces(401)
.WithTags("Health")
.RequireAuthorization();

app.MapGet("/health/detailed", async (AdamLoggerService loggerService, ITimescaleStorage timescaleStorage) =>
{
    var status = loggerService.GetStatus();
    var timescaleHealthy = await timescaleStorage.TestConnectionAsync();

    var result = new
    {
        Status = status.IsRunning && timescaleHealthy ? "Healthy" : "Unhealthy",
        Timestamp = DateTimeOffset.UtcNow,
        Components = new
        {
            Service = new
            {
                Status = status.IsRunning ? "Healthy" : "Unhealthy",
                IsRunning = status.IsRunning,
                StartTime = status.StartTime,
                Uptime = status.IsRunning ? DateTimeOffset.UtcNow - status.StartTime : TimeSpan.Zero
            },
            Database = new
            {
                Status = timescaleHealthy ? "Healthy" : "Unhealthy",
                Connected = timescaleHealthy
            },
            Devices = new
            {
                Status = status.ConnectedDevices == status.TotalDevices ? "Healthy" :
                        status.ConnectedDevices > 0 ? "Degraded" : "Unhealthy",
                Total = status.TotalDevices,
                Connected = status.ConnectedDevices,
                Details = status.DeviceHealth
            }
        }
    };

    return Results.Ok(result);
})
.WithName("GetDetailedHealth")
.WithSummary("Get detailed health status")
.WithDescription("Returns comprehensive health check including service, database, and individual device status")
.Produces<object>(200)
.Produces(401)
.WithTags("Health")
.RequireAuthorization();

// ============================================================================
// DEVICE ENDPOINTS  
// ============================================================================

app.MapGet("/devices", (AdamLoggerService loggerService) =>
{
    var status = loggerService.GetStatus();
    return Results.Ok(status.DeviceHealth);
})
.WithName("GetDevices")
.WithSummary("Get all devices status")
.WithDescription("Returns health and connectivity status for all configured ADAM devices")
.Produces<object>(200)
.Produces(401)
.WithTags("Devices")
.RequireAuthorization();

app.MapGet("/devices/{deviceId}", (string deviceId, AdamLoggerService loggerService) =>
{
    var status = loggerService.GetStatus();
    if (status.DeviceHealth.TryGetValue(deviceId, out var health))
    {
        return Results.Ok(health);
    }

    return Results.NotFound(new { Error = $"Device '{deviceId}' not found" });
})
.WithName("GetDevice")
.WithSummary("Get specific device status")
.WithDescription("Returns health and connectivity status for a specific ADAM device by ID")
.Produces<object>(200)
.Produces<object>(404)
.Produces(401)
.WithTags("Devices")
.RequireAuthorization();

app.MapPost("/devices/{deviceId}/restart", async (string deviceId, AdamLoggerService loggerService) =>
{
    try
    {
        var result = await loggerService.RestartDeviceAsync(deviceId);
        if (result)
        {
            return Results.Ok(new { Message = $"Device '{deviceId}' restarted successfully" });
        }

        return Results.NotFound(new { Error = $"Device '{deviceId}' not found" });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            title: "Device restart failed",
            statusCode: 500);
    }
})
.WithName("RestartDevice")
.WithSummary("Restart specific device")
.WithDescription("Restarts connection to a specific ADAM device to resolve connectivity issues")
.Produces<object>(200)
.Produces<object>(404)
.ProducesProblem(500)
.Produces(401)
.WithTags("Devices")
.RequireAuthorization();

// ============================================================================
// DATA ENDPOINTS
// ============================================================================

app.MapGet("/data/latest", () =>
{
    var readings = latestReadings.Values
        .OrderBy(r => r.DeviceId)
        .ThenBy(r => r.Channel)
        .ToList();

    return Results.Ok(new
    {
        Count = readings.Count,
        LastUpdated = readings.Count > 0 ? readings.Max(r => r.Timestamp) : (DateTimeOffset?)null,
        Readings = readings
    });
})
.WithName("GetLatestData")
.WithSummary("Get latest readings from all devices")
.WithDescription("Returns the most recent counter readings from all connected ADAM devices")
.Produces<object>(200)
.Produces(401)
.WithTags("Data")
.RequireAuthorization();

app.MapGet("/data/latest/{deviceId}", (string deviceId) =>
{
    var deviceReadings = latestReadings.Values
        .Where(r => r.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase))
        .OrderBy(r => r.Channel)
        .ToList();

    if (deviceReadings.Count == 0)
    {
        return Results.NotFound(new { Error = $"No readings found for device '{deviceId}'" });
    }

    return Results.Ok(new
    {
        DeviceId = deviceId,
        Count = deviceReadings.Count,
        LastUpdated = deviceReadings.Max(r => r.Timestamp),
        Readings = deviceReadings
    });
})
.WithName("GetDeviceLatestData")
.WithSummary("Get latest readings for specific device")
.WithDescription("Returns the most recent counter readings for a specific ADAM device")
.Produces<object>(200)
.Produces<object>(404)
.Produces(401)
.WithTags("Data")
.RequireAuthorization();

app.MapGet("/data/stats", (AdamLoggerService loggerService) =>
{
    var status = loggerService.GetStatus();
    var readings = latestReadings.Values.ToList();

    var deviceStats = readings.GroupBy(r => r.DeviceId)
        .Select(g => new
        {
            DeviceId = g.Key,
            ChannelCount = g.Count(),
            LastUpdate = g.Max(r => r.Timestamp),
            AverageRate = g.Where(r => r.Rate.HasValue).DefaultIfEmpty().Average(r => r?.Rate ?? 0),
            QualityDistribution = g.GroupBy(r => r.Quality)
                .ToDictionary(q => q.Key.ToString(), q => q.Count())
        }).ToList();

    return Results.Ok(new
    {
        Summary = new
        {
            ServiceRunning = status.IsRunning,
            ServiceUptime = status.IsRunning ? DateTimeOffset.UtcNow - status.StartTime : TimeSpan.Zero,
            TotalDevices = status.TotalDevices,
            ConnectedDevices = status.ConnectedDevices,
            TotalReadings = readings.Count,
            LastDataUpdate = readings.Count > 0 ? readings.Max(r => r.Timestamp) : (DateTimeOffset?)null
        },
        DeviceStatistics = deviceStats
    });
})
.WithName("GetDataStatistics")
.WithSummary("Get data collection statistics")
.WithDescription("Returns comprehensive statistics about data collection including device performance and data quality metrics")
.Produces<object>(200)
.Produces(401)
.WithTags("Data")
.RequireAuthorization();

// ============================================================================
// CONFIGURATION ENDPOINTS
// ============================================================================

app.MapGet("/config", (IConfiguration configuration) =>
{
    // Return safe configuration info (no secrets)
    var safeConfig = new
    {
        Environment = app.Environment.EnvironmentName,
        LogLevel = configuration["Logging:LogLevel:Default"],
        DemoMode = configuration.GetValue<bool>("DemoMode"),
        TimescaleDb = new
        {
            Host = configuration["TimescaleDb:Host"],
            Port = configuration.GetValue<int>("TimescaleDb:Port"),
            Database = configuration["TimescaleDb:Database"],
            TableName = configuration["TimescaleDb:TableName"],
            BatchSize = configuration.GetValue<int>("TimescaleDb:BatchSize"),
            FlushIntervalMs = configuration.GetValue<int>("TimescaleDb:FlushIntervalMs")
        }
    };

    return Results.Ok(safeConfig);
})
.WithName("GetConfiguration")
.WithSummary("Get system configuration")
.WithDescription("Returns safe system configuration settings (no sensitive data like connection strings)")
.Produces<object>(200)
.Produces(401)
.WithTags("Configuration")
.RequireAuthorization();

// ============================================================================
// UTILITY ENDPOINTS
// ============================================================================

app.MapDelete("/data/cache", () =>
{
    var count = latestReadings.Count;
    latestReadings.Clear();

    return Results.Ok(new { Message = $"Cleared {count} cached readings" });
})
.WithName("ClearDataCache")
.WithSummary("Clear cached data readings")
.WithDescription("Clears the in-memory cache of latest device readings (does not affect database storage)")
.Produces<object>(200)
.Produces(401)
.WithTags("Utilities")
.RequireAuthorization();

// Add built-in health checks endpoint
app.MapHealthChecks("/health/checks");

app.Run();

// ============================================================================
// HEALTH CHECK IMPLEMENTATIONS
// ============================================================================

/// <summary>
/// Health check for TimescaleDB connectivity
/// </summary>
public class TimescaleDbHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly ITimescaleStorage _storage;

    /// <summary>
    /// Initialize TimescaleDB health check
    /// </summary>
    /// <param name="storage">TimescaleDB storage instance</param>
    public TimescaleDbHealthCheck(ITimescaleStorage storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Check TimescaleDB connection health
    /// </summary>
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connected = await _storage.TestConnectionAsync(cancellationToken);
            return connected
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("TimescaleDB connection is healthy")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("TimescaleDB connection failed");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"TimescaleDB check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Health check for device pool connectivity
/// </summary>
public class DevicePoolHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly AdamLoggerService _service;

    /// <summary>
    /// Initialize device pool health check
    /// </summary>
    /// <param name="service">ADAM logger service instance</param>
    public DevicePoolHealthCheck(AdamLoggerService service)
    {
        _service = service;
    }

    /// <summary>
    /// Check device pool connection health
    /// </summary>
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = _service.GetStatus();
        var description = $"{status.ConnectedDevices}/{status.TotalDevices} devices connected";

        if (status.ConnectedDevices == 0 && status.TotalDevices > 0)
        {
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(description));
        }
        else if (status.ConnectedDevices < status.TotalDevices)
        {
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(description));
        }
        else
        {
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(description));
        }
    }
}
