using System;
using Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text;
using System.Diagnostics;

class JobQueueBCApp1
{
    static IConfiguration _config;
    static ILoggerFactory _loggerFactory;
    static ILogger<JobQueueBCApp1> _logger;
    static async Task<int> Main()
    {
        await Task.Delay(1);
        AppSetup();
        var s = Stopwatch.StartNew();
        var jq = new JobQueueBlockingCollection(_loggerFactory, _config);
        var tp = new Thread(() => jq.ProcessJob(CancellationToken.None)) { IsBackground = true };
        tp.Start();

        var cts = new CancellationTokenSource();
        cts.CancelAfter(5 * 1000);
        Parallel.ForEach(Enumerable.Range(1, 10), async (i) =>
        {
            _logger.LogInformation($"!!!! {i} jobs");
            string jobType = i % 2 == 0 ? "Fedex" : "UPS";
            var ti = new JobItem(jobType);
            await jq.SendJob(ti, cts.Token);
        });
        cts.CancelAfter(1 * 1000);
        await jq.FinishJob(cts.Token);

        var wis = jq.GetWastedItems();
        var fis = jq.GetFinishedItems();
        _logger.LogInformation($"1st wasted items: {wis.Count}");
        _logger.LogInformation($"1st finished items: {fis.Count}");

        cts.CancelAfter(5 * 1000);
        Parallel.ForEach(Enumerable.Range(1, 10), async (i) =>
        {
            _logger.LogInformation($"!!!! {i} jobs");
            string jobType = i % 2 == 0 ? "Fedex" : "UPS";
            var ti = new JobItem(jobType);
            await jq.SendJob(ti, cts.Token);
        });
        cts.CancelAfter(1 * 1000);
        await jq.FinishJob(cts.Token);
        wis = jq.GetWastedItems();
        fis = jq.GetFinishedItems();
        _logger.LogInformation($"2nd wasted items: {wis.Count}");
        _logger.LogInformation($"2nd finished items: {fis.Count}");

        jq.StopJobHandler();
        s.Stop();
        _logger.LogInformation($"JobQueueBCApp1 done {s.Elapsed}");
        return Environment.ExitCode;
    }
    static void AppSetup()
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
                DefaultRetryCnt = 1,
                DefaultJobQueueWaitInMillisec = 2 * 1000
            }
        });

        _config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(appsettingStr)))
                .Build();

        _loggerFactory = LoggerFactory.Create(builder =>
                builder
                .AddSerilog(logger: serilogger, dispose: true)
                );

        _logger = _loggerFactory.CreateLogger<JobQueueBCApp1>();
    }
}