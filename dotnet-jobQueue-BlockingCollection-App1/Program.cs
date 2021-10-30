using System;
using Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text;

class JobQueueBCApp1
{
    IConfiguration _config;
    ILoggerFactory _loggerFactory;
    static async Task<int> Main()
    {
        await Task.Delay(1);
        return Environment.ExitCode;
    }
    void AppSetup()
    {
        var serilogger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("serilog-jobqueue.log")
            .CreateLogger();

        var appsettingStr = JsonSerializer.Serialize(new
        {
            JobQueue = new
            {
                DefaultCapacity = 5,
                DefaultRetryCnt = 1
            }
        });

        _config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettingStr)))
                .Build();

        _loggerFactory = LoggerFactory.Create(builder =>
                builder
                .AddSerilog(logger: serilogger, dispose: true)
                );
    }
}