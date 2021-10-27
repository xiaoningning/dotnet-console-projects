using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Serilog.Sinks.File;
using Microsoft.Extensions.Configuration;

class SerilogHostApp
{
    static async Task<int> Main(string[] args)
    {
        var h = AppStartUp(args);
        var w = ActivatorUtilities.CreateInstance<WorkerService>(h.Services);
        var f = ActivatorUtilities.CreateInstance<FrontService>(h.Services);
        await Task.WhenAll(new Task[] { w.DoWork(), f.DoWork() });
        return Environment.ExitCode;
    }
    static IHost AppStartUp(string[] args)
    {
        // it must be appsettings.json
        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

        // defining Serilog configs
        var serilogLogger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("seriLog.txt")
            .CreateLogger();

        // Initiated the denpendency injection container 
        var host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices((context, services) =>
                    {
                        services.AddTransient<IWorker, WorkerService>();
                        services.AddTransient<IWorker, FrontService>();
                        services.AddLogging(config =>
                        {
                            config
                            .SetMinimumLevel(LogLevel.Information)
                            .AddSerilog(logger: serilogLogger, dispose: true);
                        });
                    })
                    .Build();

        return host;
    }
}

public interface IWorker
{
    public Task DoWork();
}
public class WorkerService : IWorker
{
    private readonly ILogger<WorkerService> _log;
    private readonly IConfiguration _config;
    public WorkerService(ILogger<WorkerService> log, IConfiguration config)
    {
        _log = log;
        _config = config;
    }
    public async Task DoWork()
    {
        // Reading App.Json
        var configVal = _config.GetSection("WorkerSerice").GetValue<string>("Param");
        var v = _config["WorkerSerice1"];
        _log.LogInformation($"DoWork Time: {DateTime.Now}");
        _log.LogInformation($"DoWork Param: {configVal}");
        _log.LogInformation($"DoWork Param1: {v}");
        await Task.Delay(100);
    }
}
public class FrontService : IWorker
{
    private readonly ILogger<FrontService> _log;
    private readonly IConfiguration _config;
    public FrontService(ILogger<FrontService> log, IConfiguration config)
    {
        _log = log;
        _config = config;
    }
    public async Task DoWork()
    {
        // Reading App.Json
        var configVal = _config.GetSection("WorkerSerice").GetValue<string>("Param");
        var v = _config["WorkerSerice1"];
        _log.LogInformation($"FrontService DoWork Time: {DateTime.Now}");
        _log.LogInformation($"FrontService DoWork Param: {configVal}");
        _log.LogInformation($"FrontService DoWork Param1: {v}");
        await Task.Delay(100);
    }
}