using Industrial.Adam.Logger.Simulator.Modbus;
using Industrial.Adam.Logger.Simulator.Simulation;
using Industrial.Adam.Logger.Simulator.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ADAM-6051 Simulator API",
        Version = "v1",
        Description = "Control API for ADAM-6051 counter module simulator",
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
    var xmlPath = Path.Combine(AppContext.BaseDirectory, "Industrial.Adam.Logger.Simulator.xml");
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add simulator services
builder.Services.AddSingleton<Adam6051RegisterMap>();
builder.Services.AddSingleton<Adam6051ModbusServer>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var modbusPort = config.GetValue<int>("SimulatorSettings:ModbusPort", 502);

    return new Adam6051ModbusServer(
        provider.GetRequiredService<Adam6051RegisterMap>(),
        provider.GetRequiredService<ILogger<Adam6051ModbusServer>>(),
        modbusPort);
});
builder.Services.AddSingleton<SimulatorDatabase>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var dbPath = config["SimulatorSettings:DatabasePath"] ?? "data/simulator.db";

    // Ensure directory exists
    var directory = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    return new SimulatorDatabase(
        dbPath,
        provider.GetRequiredService<ILogger<SimulatorDatabase>>());
});

builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddHostedService<SimulationEngine>();

// Add Modbus server as hosted service
builder.Services.AddHostedService<ModbusServerHost>();

// Configure Kestrel to use the API port from configuration
builder.WebHost.ConfigureKestrel(options =>
{
    var config = builder.Configuration;
    var apiPort = config.GetValue<int>("SimulatorSettings:ApiPort", 8080);
    options.ListenAnyIP(apiPort);
});

var app = builder.Build();

// Configure pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

// Log startup info
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var config = app.Services.GetRequiredService<IConfiguration>();

logger.LogInformation("ADAM-6051 Simulator starting");
logger.LogInformation("Device ID: {DeviceId}", config["SimulatorSettings:DeviceId"]);
logger.LogInformation("Modbus Port: {Port}", config["SimulatorSettings:ModbusPort"]);
logger.LogInformation("API Port: {Port}", config["SimulatorSettings:ApiPort"]);

app.Run();

// Hosted service wrapper for Modbus server
public class ModbusServerHost : IHostedService
{
    private readonly Adam6051ModbusServer _modbusServer;

    public ModbusServerHost(Adam6051ModbusServer modbusServer)
    {
        _modbusServer = modbusServer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _modbusServer.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _modbusServer.StopAsync();
    }
}
