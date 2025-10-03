using Industrial.Adam.Logger.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Console;

/// <summary>
/// Console application for ADAM device logging
/// </summary>
internal class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            // Create host builder
            var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add logging
                services.AddLogging(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddConsole();
                    builder.AddFilter("Industrial.Adam.Logger.Core", LogLevel.Debug);
                });

                // Add ADAM logger from configuration
                services.AddAdamLogger(context.Configuration);

                // Or use demo configuration for testing
                // services.AddAdamLoggerDemo();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();

                // Configure log levels from appsettings.json
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            })
            .Build();

            // Handle Ctrl+C gracefully
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            lifetime.ApplicationStarted.Register(() =>
            {
                logger.LogInformation("===========================================");
                logger.LogInformation("ADAM Logger Service Started");
                logger.LogInformation("Press Ctrl+C to stop");
                logger.LogInformation("===========================================");
            });

            lifetime.ApplicationStopping.Register(() =>
            {
                logger.LogInformation("===========================================");
                logger.LogInformation("ADAM Logger Service Stopping...");
                logger.LogInformation("===========================================");
            });

            lifetime.ApplicationStopped.Register(() =>
            {
                logger.LogInformation("ADAM Logger Service Stopped");
            });

            // Run the host
            await host.RunAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Configuration validation failed"))
        {
            // Configuration validation errors - show user-friendly message
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine("❌ Configuration Error");
            System.Console.WriteLine("═══════════════════════");
            System.Console.ResetColor();
            System.Console.WriteLine(ex.Message);
            System.Console.WriteLine();

            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine("💡 Quick Fix Guide:");
            System.Console.WriteLine("• Check your appsettings.json file structure");
            System.Console.WriteLine("• Ensure TimescaleDB settings are under 'AdamLogger:TimescaleDb'");
            System.Console.WriteLine("• Verify all required fields are present");
            System.Console.WriteLine("• See documentation for complete examples");
            System.Console.ResetColor();

            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            // Other startup errors
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine("❌ Startup Failed");
            System.Console.WriteLine("═════════════════");
            System.Console.ResetColor();
            System.Console.WriteLine($"Error: {ex.Message}");

            if (ex.InnerException != null)
            {
                System.Console.WriteLine($"Details: {ex.InnerException.Message}");
            }

            System.Console.WriteLine();
            System.Console.ForegroundColor = ConsoleColor.Gray;
            System.Console.WriteLine("For detailed error information, check the logs or run with --verbosity detailed");
            System.Console.ResetColor();

            Environment.Exit(1);
        }
    }
}
