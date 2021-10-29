using System;
using System.IO;
using Serilog;
using Serilog.Debugging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text;


public class SerilogConsoleApp
{

    public static void Main(string[] args)
    {
        // SelfLog.Enable(Console.Out);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("serilog.log")
            .CreateLogger();

        for (var i = 0; i < 1; ++i)
        {
            Log.Information($"Hello, file logger! {DateTime.Now}");
        }


        sw.Stop();

        Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Size: {new FileInfo("serilog.log").Length}");

        var appsettingStr = JsonSerializer.Serialize(new
        {
            WorkService = "WorkServiceValue1",
            FrontService = "FrontServiceValue1"
        });
        var config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettingStr))).Build();

        var ws = new WorkerService(Log.Logger, config);
        ws.DoWork();

        var logger = new SerilogLoggerProvider(Log.Logger, true);
        var lf = LoggerFactory.Create(builder =>
                        builder
                        .AddSerilog(logger: Log.Logger, dispose: true));
        var fs = new FrontService(lf, config);
        fs.DoWork();

        var consoleLf = LoggerFactory.Create(builder =>
                        builder
                        .AddSimpleConsole(options =>
                            {
                                options.IncludeScopes = true;
                                options.SingleLine = true;
                                options.TimestampFormat = "hh:mm:ss ";
                            }));
        var fs1 = new FrontService(consoleLf, config);
        fs1.DoWork();

        Log.CloseAndFlush();
    }
}

public class WorkerService
{
    readonly Serilog.ILogger _logger;
    readonly IConfiguration _config;
    public WorkerService(Serilog.ILogger logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _logger.Information($"iconfiguration: {_config.GetSection("WorkService").Value}");
    }
    public void DoWork()
    {
        _logger.Information("do work");
    }
}

public class FrontService
{
    readonly ILogger<FrontService> _logger;
    readonly IConfiguration _config;
    public FrontService(ILoggerFactory logger, IConfiguration config)
    {
        _logger = logger?.CreateLogger<FrontService>() ?? NullLoggerFactory.Instance.CreateLogger<FrontService>();
        _config = config;
        _logger.LogInformation($"iconfiguration: {_config.GetSection("FrontService").Value}");
    }
    public void DoWork()
    {
        _logger.LogInformation("do work");
    }
}