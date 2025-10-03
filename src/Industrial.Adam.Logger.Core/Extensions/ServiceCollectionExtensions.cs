using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Devices;
using Industrial.Adam.Logger.Core.Processing;
using Industrial.Adam.Logger.Core.Services;
using Industrial.Adam.Logger.Core.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Industrial.Adam.Logger.Core.Extensions;

/// <summary>
/// Service collection extensions for ADAM logger
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add ADAM logger services to the service collection
    /// </summary>
    public static IServiceCollection AddAdamLogger(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validate configuration structure early
        ValidateConfigurationStructure(configuration);

        // Add configuration
        services.Configure<LoggerConfiguration>(configuration.GetSection("AdamLogger"));
        services.Configure<TimescaleSettings>(configuration.GetSection("AdamLogger:TimescaleDb"));

        // Add core services
        services.AddSingleton<DeviceHealthTracker>();
        services.AddSingleton<ModbusDevicePool>();

        // Add data processing
        services.AddSingleton<IDataProcessor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DataProcessor>>();
            var config = provider.GetRequiredService<IOptions<LoggerConfiguration>>().Value;
            return new DataProcessor(logger, config);
        });

        // Add storage
        services.AddSingleton<ITimescaleStorage>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<TimescaleStorage>>();
            var settings = provider.GetRequiredService<IOptions<TimescaleSettings>>().Value;
            return new TimescaleStorage(logger, settings);
        });

        // Add main service
        services.AddHostedService<AdamLoggerService>();
        services.AddSingleton<AdamLoggerService>(provider =>
            provider.GetServices<IHostedService>()
                .OfType<AdamLoggerService>()
                .First());

        return services;
    }

    /// <summary>
    /// Validates the configuration structure to provide helpful error messages
    /// </summary>
    private static void ValidateConfigurationStructure(IConfiguration configuration)
    {
        var errors = new List<string>();

        // Check if AdamLogger section exists
        var adamLoggerSection = configuration.GetSection("AdamLogger");
        if (!adamLoggerSection.Exists())
        {
            errors.Add("Missing 'AdamLogger' configuration section in appsettings.json. " +
                      "The configuration must be structured as: { \"AdamLogger\": { \"Devices\": [...], \"TimescaleDb\": {...} } }");
        }
        else
        {
            // Check for common mistake: TimescaleDb at root level
            var rootTimescaleDb = configuration.GetSection("TimescaleDb");
            if (rootTimescaleDb.Exists() && !adamLoggerSection.GetSection("TimescaleDb").Exists())
            {
                errors.Add("TimescaleDB configuration found at root level but should be nested under 'AdamLogger'. " +
                          "Move 'TimescaleDb' section inside 'AdamLogger' section: { \"AdamLogger\": { \"TimescaleDb\": {...} } }");
            }

            // Check if TimescaleDb section exists under AdamLogger
            var timescaleDbSection = adamLoggerSection.GetSection("TimescaleDb");
            if (!timescaleDbSection.Exists())
            {
                errors.Add("Missing 'AdamLogger:TimescaleDb' configuration section. " +
                          "Add TimescaleDB settings under AdamLogger: { \"AdamLogger\": { \"TimescaleDb\": { \"Url\": \"...\", \"Token\": \"...\" } } }");
            }

            // Check if Devices section exists
            var devicesSection = adamLoggerSection.GetSection("Devices");
            if (!devicesSection.Exists() || !devicesSection.GetChildren().Any())
            {
                errors.Add("Missing or empty 'AdamLogger:Devices' configuration section. " +
                          "Add at least one device: { \"AdamLogger\": { \"Devices\": [{ \"DeviceId\": \"...\", \"IpAddress\": \"...\" }] } }");
            }
        }

        if (errors.Any())
        {
            var message = "Configuration validation failed:\n" + string.Join("\n", errors.Select(e => "  • " + e));
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Add ADAM logger with custom configuration
    /// </summary>
    public static IServiceCollection AddAdamLogger(
        this IServiceCollection services,
        Action<LoggerConfiguration> configureLogger,
        Action<TimescaleSettings> configureInflux)
    {
        // Add configuration with actions
        services.Configure(configureLogger);
        services.Configure(configureInflux);

        // Add core services
        services.AddSingleton<DeviceHealthTracker>();
        services.AddSingleton<ModbusDevicePool>();

        // Add data processing
        services.AddSingleton<IDataProcessor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DataProcessor>>();
            var config = provider.GetRequiredService<IOptions<LoggerConfiguration>>().Value;
            return new DataProcessor(logger, config);
        });

        // Add storage
        services.AddSingleton<ITimescaleStorage>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<TimescaleStorage>>();
            var settings = provider.GetRequiredService<IOptions<TimescaleSettings>>().Value;
            return new TimescaleStorage(logger, settings);
        });

        // Add main service
        services.AddHostedService<AdamLoggerService>();
        services.AddSingleton<AdamLoggerService>(provider =>
            provider.GetServices<IHostedService>()
                .OfType<AdamLoggerService>()
                .First());

        return services;
    }

    /// <summary>
    /// Add ADAM logger with demo/test configuration
    /// </summary>
    public static IServiceCollection AddAdamLoggerDemo(
        this IServiceCollection services,
        string timescaleHost = "localhost",
        string timescaleDatabase = "adam_demo")
    {
        return services.AddAdamLogger(
            logger =>
            {
                logger.Devices = new List<DeviceConfig>
                {
                    new DeviceConfig
                    {
                        DeviceId = "DEMO001",
                        Name = "Demo ADAM Device",
                        IpAddress = "127.0.0.1",
                        Port = 502,
                        UnitId = 1,
                        Enabled = true,
                        PollIntervalMs = 1000,
                        TimeoutMs = 3000,
                        MaxRetries = 3,
                        Channels = new List<ChannelConfig>
                        {
                            new ChannelConfig
                            {
                                ChannelNumber = 0,
                                Name = "Demo Counter",
                                StartRegister = 0,
                                RegisterCount = 2,
                                Enabled = true,
                                ScaleFactor = 1.0,
                                Unit = "counts"
                            }
                        }
                    }
                };
            },
            timescale =>
            {
                timescale.Host = timescaleHost;
                timescale.Database = timescaleDatabase;
                timescale.Username = "demo";
                timescale.Password = "demo";
                timescale.TableName = "counter_data";
                timescale.BatchSize = 10;
                timescale.FlushIntervalMs = 5000;
            });
    }
}
