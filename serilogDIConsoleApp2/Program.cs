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

class SerilogConsoleApp2
{
    static ILogger<SerilogConsoleApp2>? _logger;
    static async Task<int> Main(string[] args)
    {
        var builder = AppSetup(args);

        var ws = builder.GetService<WorkerService>();
        var rand = new Random();
        await ws?.DoWork(rand.Next().ToString());

        _logger = builder.GetRequiredService<ILogger<SerilogConsoleApp2>>();
        _logger.LogInformation($"main logger");

        var config = builder.GetService<IConfiguration>();
        _logger.LogInformation("Iconfiguration read in main: {pv}", config?["WorkerSerice:Param"]);
        builder.Dispose();

        return Environment.ExitCode;
    }
    static ServiceProvider AppSetup(string[] args)
    {
        IConfiguration iconfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

        // setup serilog config
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .WriteTo.File("seriLogConsoleApp2.log")
            .CreateLogger();

        // Setting up dependency injection
        var serviceCollection = new ServiceCollection();

        /**
        // Creating a `LoggerProviderCollection` lets Serilog optionally write
        // events through other dynamically-added MEL ILoggerProviders.
        // not Working !!! for some reason
        var providers = new LoggerProviderCollection();
        serviceCollection.AddSingleton(providers);
        serviceCollection.AddSingleton<ILoggerFactory>(sc =>
        {
            var providerCollection = sc.GetService<LoggerProviderCollection>();
            var factory = new SerilogLoggerFactory(null, true, providerCollection);
            foreach (var provider in sc.GetServices<ILoggerProvider>()) factory.AddProvider(provider);
            return factory;
        });
        */
        serviceCollection.AddSingleton(iconfig);
        serviceCollection.AddLogging(config =>
        {
            config
            .ClearProviders()
            .AddSerilog(logger: serilogLogger, dispose: true);
        });
        serviceCollection.AddTransient<WorkerService>();

        return serviceCollection.BuildServiceProvider(validateScopes: true);
    }
}

public class WorkerService
{
    readonly ILogger<WorkerService> _logger;
    readonly IConfiguration _config;
    public WorkerService(ILogger<WorkerService> logger, IConfiguration config)
    {
        if (logger == null)
        {
            Console.WriteLine("Logger is null (Console), using NullLogger");
            Trace.TraceWarning("Logger is null (Trace), using NullLogger");
            Debug.WriteLine("Logger is null (Debug),using NullLogger");
        }

        _logger = logger ?? NullLogger<WorkerService>.Instance;
        _logger.LogDebug("Logger OK (logger)");
        _config = config;
        string configv = _config.GetSection("WorkerSerice").GetSection("Param").Value;
        _logger.LogDebug($"Iconfiguration: {configv}");
    }

    public async Task DoWork(string x)
    {
        _logger.LogInformation($"{this.GetType()} {x}");
        await Task.Delay(1000);

        try
        {
            throw new Exception("Boom!");
        }
        catch (Exception ex)
        {
            _logger.LogCritical("Unexpected critical error starting application", ex);
            _logger.Log(LogLevel.Critical, 0, "Unexpected critical error", ex, null);
            _logger.LogError("Unexpected error", ex);
            _logger.LogWarning("Unexpected warning", ex);
        }

        using (_logger.BeginScope("Main"))
        {
            _logger.LogInformation("log scope");
        }
    }
}