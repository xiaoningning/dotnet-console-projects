using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.File;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
class JobQueueTPLApp1
{
    static readonly string _logFileName = "log-jobQueueTPLApp1.txt";
    static readonly string _appSettingFileName = "appsettings.json";
    static async Task<int> Main(string[] args)
    {
        await Task.Delay(1 * 1000);
        return Environment.ExitCode;
    }
    static ServiceProvider AppSetup()
    {
        var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(_appSettingFileName)
                .AddEnvironmentVariables()
                .Build();

        // setup serilog config
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .WriteTo.File(_logFileName)
            .CreateLogger();

        // Setting up dependency injection
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(configBuilder);
        serviceCollection.AddLogging(config =>
        {
            config
            .ClearProviders()
            .AddSerilog(logger: serilogLogger, dispose: true);
        });
        // Add service 
        return serviceCollection.BuildServiceProvider(validateScopes: true);
    }
}