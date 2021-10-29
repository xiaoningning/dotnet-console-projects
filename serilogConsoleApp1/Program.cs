using System;
using System.IO;
using Serilog;
using Serilog.Debugging;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;


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

        // var logger = new SerilogLoggerProvider(Log.Logger, true).CreateLogger(typeof(WorkerService).FullName);
        var ws = new WorkerService(Log.Logger);
        ws.DoWork();
        Log.CloseAndFlush();
    }
}

public class WorkerService
{
    readonly Serilog.ILogger _logger;
    public WorkerService(Serilog.ILogger logger)
    {
        _logger = logger;
    }
    public void DoWork()
    {
        _logger.Information("do work");
    }
}